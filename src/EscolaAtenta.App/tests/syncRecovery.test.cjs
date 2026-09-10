const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm'),ts=require('typescript');
function carregar({get=async()=>{throw Error('rede');}, chamada=async()=>{throw Error('rede');}}={}){
 const mod={exports:{}};
 const source=fs.readFileSync('src/services/sync/watermelondbSync.ts','utf8')+'\nexport { reverterAlteracoesRejeitadas, restaurarPresencaDoServidor };';
 vm.runInNewContext(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2020}}).outputText,{
  exports:mod.exports,module:mod,console:{log(){},warn(){}},require(name){
   if(name==='../api')return {api:{get}};
   if(name==='../chamadasService')return {chamadasService:{obterChamadaPorDia:chamada}};
   if(name==='../sessionScope')return {sessionScope:{active:false}};
   return {};
  }
 });return mod.exports;
}
function registro(){return {id:'t1',nome:'Rejeitado',turno:'Tarde',anoLetivo:2099,turmaId:'t1',alunoId:'a1',data:new Date(),status:'Falta',_raw:{_status:'updated',_changed:'nome'},apagado:false,
 async update(f){f(this);this._raw._status='updated';},async destroyPermanently(){this.apagado=true;}};}
function banco(r){return {get:()=>({find:async()=>r}),write:async f=>f()};}
const payload={turmasCreated:[],turmasUpdated:[{id:'t1'}],alunosCreated:[],presencasCreated:[],presencasUpdated:[],rejeicoes:[{idExterno:'t1',motivo:'Inválido'}]};
test('turma rejeitada recebe valores do servidor antes de limpar pendência',async()=>{
 const r=registro();let full=false;
 const sut=carregar({get:async(url,opts)=>{full=url==='/sync/pull'&&opts.params.lastPulledAt===0;return {data:{changes:{turmas:{created:[{id:'t1',nome:'Oficial',turno:'Manhã',ano_letivo:2026}],updated:[],deleted:[]}}}};}});
 await sut.reverterAlteracoesRejeitadas(banco(r),payload);
 assert.equal(full,true);assert.equal(r.nome,'Oficial');assert.equal(r.turno,'Manhã');assert.equal(r.anoLetivo,2026);assert.equal(r._raw._status,'synced');
});
test('falha ao recuperar turma mantém edição pendente para nova tentativa',async()=>{
 const r=registro();await carregar().reverterAlteracoesRejeitadas(banco(r),payload);assert.equal(r._raw._status,'updated');assert.equal(r.apagado,false);
});
for(const tipo of ['offline','timeout','HTTP 500'])test('recuperação de presença preserva linha em '+tipo,async()=>{
 const r=registro();await carregar({chamada:async()=>{throw Object.assign(Error(tipo),{response:tipo==='HTTP 500'?{status:500}:undefined});}}).restaurarPresencaDoServidor(banco(r),r.id);
 assert.equal(r.apagado,false);assert.equal(r._raw._status,'updated');
});
test('presença confirmada ausente no servidor pode ser removida',async()=>{
 const r=registro();await carregar({chamada:async()=>null}).restaurarPresencaDoServidor(banco(r),r.id);assert.equal(r.apagado,true);
});
test('presença recuperada restaura estado oficial',async()=>{
 const r=registro();await carregar({chamada:async()=>({registros:[{alunoId:'a1',status:'Presente'}]})}).restaurarPresencaDoServidor(banco(r),r.id);
 assert.equal(r.apagado,false);assert.equal(r.status,'Presente');assert.equal(r._raw._status,'synced');
});
