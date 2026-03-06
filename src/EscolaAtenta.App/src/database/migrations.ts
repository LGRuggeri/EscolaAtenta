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
  ],
});
