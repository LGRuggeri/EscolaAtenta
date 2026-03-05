import { Model } from '@nozbe/watermelondb';
import { text, field, date, immutableRelation, writer } from '@nozbe/watermelondb/decorators';

export type StatusPresencaLocal = 'Presente' | 'Falta' | 'Atraso' | 'FaltaJustificada';

export default class RegistroPresenca extends Model {
  static table = 'registros_presenca';

  static associations = {
    alunos: { type: 'belongs_to' as const, key: 'aluno_id' },
    turmas: { type: 'belongs_to' as const, key: 'turma_id' },
  };

  @text('aluno_id') alunoId!: string;
  @text('turma_id') turmaId!: string;
  @date('data') data!: Date;
  @text('status') status!: StatusPresencaLocal;
  @field('sincronizado') sincronizado!: boolean;

  @immutableRelation('alunos', 'aluno_id') aluno: any;
  @immutableRelation('turmas', 'turma_id') turma: any;

  /**
   * Altera o status de presença localmente e marca como não-sincronizado.
   * Usado pelo monitor para corrigir uma chamada antes do envio ao servidor.
   */
  @writer async alterarStatus(novoStatus: StatusPresencaLocal) {
    await this.update((registro) => {
      registro.status = novoStatus;
      registro.sincronizado = false;
    });
  }

  /**
   * Marca o registro como sincronizado com sucesso com a API.
   * Chamado pelo serviço de sync após confirmação do backend.
   */
  @writer async marcarSincronizado() {
    await this.update((registro) => {
      registro.sincronizado = true;
    });
  }
}
