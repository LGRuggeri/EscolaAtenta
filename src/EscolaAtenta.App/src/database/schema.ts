import { appSchema, tableSchema } from '@nozbe/watermelondb';

export default appSchema({
  version: 1,
  tables: [
    tableSchema({
      name: 'turmas',
      columns: [
        { name: 'nome', type: 'string' },
        { name: 'turno', type: 'string' },
      ],
    }),
    tableSchema({
      name: 'alunos',
      columns: [
        { name: 'nome', type: 'string' },
        { name: 'turma_id', type: 'string', isIndexed: true },
      ],
    }),
    tableSchema({
      name: 'registros_presenca',
      columns: [
        { name: 'aluno_id', type: 'string', isIndexed: true },
        { name: 'turma_id', type: 'string', isIndexed: true },
        { name: 'data', type: 'number' }, // timestamp (epoch ms)
        { name: 'status', type: 'string' }, // Presente | Falta | Atraso
        { name: 'sincronizado', type: 'boolean' },
      ],
    }),
  ],
});
