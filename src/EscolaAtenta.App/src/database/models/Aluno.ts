import { Model } from '@nozbe/watermelondb';
import { text, immutableRelation } from '@nozbe/watermelondb/decorators';

export default class Aluno extends Model {
  static table = 'alunos';

  static associations = {
    turmas: { type: 'belongs_to' as const, key: 'turma_id' },
  };

  @text('nome') nome!: string;
  @text('turma_id') turmaId!: string;

  @immutableRelation('turmas', 'turma_id') turma: any;
}
