import { appSchema, tableSchema } from '@nozbe/watermelondb';

export default appSchema({
  version: 4,
  tables: [
    tableSchema({
      name: 'turmas',
      columns: [
        { name: 'nome', type: 'string' },
        { name: 'turno', type: 'string' },
        { name: 'ano_letivo', type: 'number' },
        { name: 'server_id', type: 'string', isOptional: true },
      ],
    }),
    tableSchema({
      name: 'alunos',
      columns: [
        { name: 'nome', type: 'string' },
        { name: 'turma_id', type: 'string', isIndexed: true },
        { name: 'faltas_consecutivas_atuais', type: 'number' },
        { name: 'total_faltas', type: 'number' },
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
