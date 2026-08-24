import { Model } from '@nozbe/watermelondb';
import { text, field, immutableRelation } from '@nozbe/watermelondb/decorators';

export default class Aluno extends Model {
  static table = 'alunos';

  static associations = {
    turmas: { type: 'belongs_to' as const, key: 'turma_id' },
  };

  @text('nome') nome!: string;
  @text('turma_id') turmaId!: string;
  @text('server_id') serverId!: string | null;
  @text('matricula') matricula!: string | null;
  @field('faltas_consecutivas_atuais') faltasConsecutivasAtuais!: number;
  @field('total_faltas') totalFaltas!: number;

  @immutableRelation('turmas', 'turma_id') turma: any;
}
