import { Database } from '@nozbe/watermelondb';
import SQLiteAdapter from '@nozbe/watermelondb/adapters/sqlite';

import schema from './schema';
import migrations from './migrations';
import Turma from './models/Turma';
import Aluno from './models/Aluno';
import RegistroPresenca from './models/RegistroPresenca';

const adapter = new SQLiteAdapter({
  schema,
  migrations,
  jsi: true,
  onSetUpError: (error) => {
    console.error('[WatermelonDB] Falha ao inicializar banco local:', error);
  },
});

const database = new Database({
  adapter,
  modelClasses: [Turma, Aluno, RegistroPresenca],
});

export default database;
