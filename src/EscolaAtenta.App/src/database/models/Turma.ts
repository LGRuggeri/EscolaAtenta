import { Model } from '@nozbe/watermelondb';
import { text, field, children } from '@nozbe/watermelondb/decorators';

export default class Turma extends Model {
  static table = 'turmas';

  static associations = {
    alunos: { type: 'has_many' as const, foreignKey: 'turma_id' },
    registros_presenca: { type: 'has_many' as const, foreignKey: 'turma_id' },
  };

  @text('nome') nome!: string;
  @text('turno') turno!: string;
  @field('ano_letivo') anoLetivo!: number;
  @text('server_id') serverId!: string;

  @children('alunos') alunos: any;
  @children('registros_presenca') registrosPresenca: any;
}
