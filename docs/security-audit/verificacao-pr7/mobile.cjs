// Executa as funções reais de recuperação com rede e banco simulados.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const app = path.resolve(__dirname, '../../../src/EscolaAtenta.App');
const ts = require(path.join(app, 'node_modules/typescript'));
let erroRede;
let chamadasRede = 0;
const chamadasService = {obterChamadaPorDia: async () => {chamadasRede++; throw erroRede;}};
const source = fs.readFileSync(path.join(app,'src/services/sync/watermelondbSync.ts'),'utf8') + '\nexport { reverterAlteracoesRejeitadas, restaurarPresencaDoServidor };';
const mod = {exports:{}};
vm.runInNewContext(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2020}}).outputText, {
 exports:mod.exports,module:mod,console:{log(){},warn(){}},require(name){
  if(name==='../chamadasService')return {chamadasService};
  if(name==='../sessionScope')return {sessionScope:{active:false}};
  return {};
 }
});
const {reverterAlteracoesRejeitadas,restaurarPresencaDoServidor}=mod.exports;
function banco(record){return {get:()=>({find:async()=>record}),write:async f=>f()};}
(async()=>{
 const turma={id:'t1',nome:'Nome rejeitado',turno:'Turno rejeitado',anoLetivo:2099,_raw:{_status:'updated',_changed:'nome,turno,ano_letivo'}};
 await reverterAlteracoesRejeitadas(banco(turma),{turmasCreated:[],turmasUpdated:[{id:'t1'}],alunosCreated:[],presencasCreated:[],presencasUpdated:[],rejeicoes:[{idExterno:'t1',motivo:'Atualização inválida'}]});
 const turmaReproduzida=turma.nome==='Nome rejeitado' && turma._raw._status==='synced' && chamadasRede===0;
 console.log(JSON.stringify({caso:'P2 turma rejeitada',reproduzido:turmaReproduzida,nome:turma.nome,estado:turma._raw._status,consultasServidor:chamadasRede}));
 if(!turmaReproduzida)process.exitCode=1;
 for(const tipo of ['offline','timeout','HTTP 500']){
  erroRede=new Error(tipo); if(tipo==='HTTP 500')erroRede.response={status:500}; if(tipo==='timeout')erroRede.code='ECONNABORTED';
  let destruido=false;
  const registro={id:'p1',turmaId:'t1',alunoId:'a1',data:new Date(),destroyPermanently:async()=>{destruido=true;}};
  await restaurarPresencaDoServidor(banco(registro),'p1');
  console.log(JSON.stringify({caso:'P2 recuperação de presença',falha:tipo,reproduzido:destruido,presencaApagada:destruido}));
  if(!destruido)process.exitCode=1;
 }
})().catch(e=>{console.error(e);process.exitCode=1;});
