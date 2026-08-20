import { schemaMigrations, addColumns, unsafeExecuteSql } from '@nozbe/watermelondb/Schema/migrations';

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
    {
      toVersion: 4,
      steps: [
        // Remove os contadores trimestrais descontinuados. SQLite ≥ 3.35 suporta DROP COLUMN;
        // em versões anteriores a migration falharia, mas o WatermelonDB faria fallback
        // recriando o banco caso necessário. Mantém dados essenciais (id, nome, turma_id,
        // faltas_consecutivas_atuais, total_faltas).
        unsafeExecuteSql(
          'ALTER TABLE alunos DROP COLUMN faltas_no_trimestre;'
        ),
        unsafeExecuteSql(
          'ALTER TABLE alunos DROP COLUMN atrasos_no_trimestre;'
        ),
      ],
    },
  ],
});
