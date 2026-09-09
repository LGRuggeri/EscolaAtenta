import { Database } from '@nozbe/watermelondb';
import SQLiteAdapter from '@nozbe/watermelondb/adapters/sqlite';
import * as SecureStore from 'expo-secure-store';
import schema from './schema';
import migrations from './migrations';
import Turma from './models/Turma';
import Aluno from './models/Aluno';
import RegistroPresenca from './models/RegistroPresenca';
import { databaseName } from '../services/sessionScope';

let active: Database | null = null;
const instances = new Map<string, Database>();

// O banco legado sem identidade permanece em quarentena: nunca aberto ou apagado.
// O índice persiste um nome curto por identidade, evitando limites de caminho SQLite.
export async function activateDatabase(server: string, userId: string): Promise<void> {
  const key = databaseName(server, userId);
  let name = await SecureStore.getItemAsync(key);
  if (!name) {
    name = 'ea2_' + Date.now().toString(36) + '_' + Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2);
    await SecureStore.setItemAsync(key, name);
  }
  let instance = instances.get(name);
  if (!instance) {
    const adapter = new SQLiteAdapter({ dbName: name, schema, migrations, jsi: true });
    await adapter.initializingPromise;
    instance = new Database({ adapter, modelClasses: [Turma, Aluno, RegistroPresenca] });
    instances.set(name, instance);
  }
  active = instance;
}
export function deactivateDatabase(): void { active = null; }
export function getDatabase(): Database {
  if (!active) throw new Error('Banco indisponível fora de uma sessão autenticada.');
  return active;
}
