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
        // As colunas faltas_no_trimestre e atrasos_no_trimestre não são mais usadas pelo
        // schema v4. Em vez de executar ALTER TABLE ... DROP COLUMN — incompatível com
        // SQLite < 3.35 e capaz de corromper registros de presença offline caso o adapter
        // recrie o banco — mantemos as colunas físicas legadas no SQLite. O WatermelonDB
        // aceita colunas extras na tabela desde que todas as colunas do schema atual estejam
        // presentes, preservando assim os dados offline em todos os dispositivos.
        unsafeExecuteSql(
          'SELECT 1; -- v4: colunas trimestrais tornaram-se legadas e não são mais lidas/escritas'
        ),
      ],
    },
    {
      toVersion: 5,
      steps: [
        addColumns({
          table: 'alunos',
          columns: [
            { name: 'server_id', type: 'string', isOptional: true },
            { name: 'matricula', type: 'string', isOptional: true },
          ],
        }),
      ],
    },
  ],
});
