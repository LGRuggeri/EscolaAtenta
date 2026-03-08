import { schemaMigrations, addColumns } from '@nozbe/watermelondb/Schema/migrations';

export default schemaMigrations({
  migrations: [
    {
      toVersion: 2,
      steps: [
        addColumns({
          table: 'turmas',
          columns: [
            { name: 'ano_letivo', type: 'number' },
            { name: 'server_id', type: 'string', isOptional: true },
          ],
        }),
      ],
    },
    {
      toVersion: 3,
      steps: [
        addColumns({
          table: 'alunos',
          columns: [
            { name: 'faltas_consecutivas_atuais', type: 'number' },
            { name: 'faltas_no_trimestre', type: 'number' },
            { name: 'total_faltas', type: 'number' },
            { name: 'atrasos_no_trimestre', type: 'number' },
          ],
        }),
      ],
    },
  ],
});
