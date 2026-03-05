import { Model } from '@nozbe/watermelondb';
import { text, children } from '@nozbe/watermelondb/decorators';

export default class Turma extends Model {
  static table = 'turmas';

  static associations = {
    alunos: { type: 'has_many' as const, foreignKey: 'turma_id' },
    registros_presenca: { type: 'has_many' as const, foreignKey: 'turma_id' },
  };

  @text('nome') nome!: string;
  @text('turno') turno!: string;

  @children('alunos') alunos: any;
  @children('registros_presenca') registrosPresenca: any;
}
