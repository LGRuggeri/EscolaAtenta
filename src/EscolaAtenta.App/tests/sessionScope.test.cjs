const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const ts=require('typescript');
const vm=require('node:vm');
const exportsModule={exports:{}};
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/services/sessionScope.ts','utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2020}}).outputText,{exports:exportsModule.exports, module:exportsModule, Set, Error});
const {databaseName, SessionScope}=exportsModule.exports;
test('separa conta e servidor sem colisões e normaliza barra final',()=>{
 assert.notEqual(databaseName('http://escola:5114','a'),databaseName('http://escola:5114','b'));
 assert.notEqual(databaseName('http://escola:5114','a'),databaseName('http://outra:5114','a'));
 assert.equal(databaseName('http://escola:5114/','a'),databaseName('http://escola:5114','a'));
 assert.throws(()=>databaseName('','a'));
});
test('troca invalida operações antigas e espera ciclo antes de liberar',async()=>{
 const scope=new SessionScope();
 const old=scope.generation;
 let finish; const pending=new Promise(resolve=>finish=resolve);
 scope.track(pending);
 let complete=false; const drain=scope.invalidate().then(()=>complete=true);
 assert.throws(()=>scope.assert(old));
 await Promise.resolve(); assert.equal(complete,false);
 finish(); await drain; assert.equal(complete,true);
});
test('isola instâncias e preserva pendências da conta ao voltar',async()=>{
 const stored=new Map();
 const secure={getItemAsync:async key=>stored.get(key)??null,setItemAsync:async(key,value)=>stored.set(key,value)};
 class Adapter {constructor(options){this.name=options.dbName;this.initializingPromise=Promise.resolve();}}
 class Database {constructor(options){this.name=options.adapter.name;this.pending=[];}}
 const moduleDb={exports:{}};
 vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/database/index.ts','utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2020}}).outputText,{
   exports:moduleDb.exports,module:moduleDb,require:name=>{
    if(name==='expo-secure-store')return secure;
    if(name==='@nozbe/watermelondb')return {Database};
    if(name==='@nozbe/watermelondb/adapters/sqlite')return {default:Adapter};
    if(name==='../services/sessionScope')return {databaseName};
    return {default:{}};
   }
 });
 const db=moduleDb.exports;
 assert.throws(()=>db.getDatabase());
 await db.activateDatabase('http://escola','a'); const a=db.getDatabase();a.pending.push('chamada A');
 db.deactivateDatabase();assert.throws(()=>db.getDatabase());
 await db.activateDatabase('http://escola','b');assert.notEqual(db.getDatabase(),a);assert.equal(db.getDatabase().pending.length,0);
 await db.activateDatabase('http://outra','a');assert.notEqual(db.getDatabase(),a);
 await db.activateDatabase('http://escola','a');assert.equal(db.getDatabase(),a);assert.equal(a.pending[0],'chamada A');
});
test('identidades unicode distintas não colidem',()=>{
 assert.notEqual(databaseName('http://escola','😀'),databaseName('http://escola','😁'));
});
