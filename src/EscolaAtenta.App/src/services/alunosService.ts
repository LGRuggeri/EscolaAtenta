import { Q } from '@nozbe/watermelondb';
import database from '../database';
import Aluno from '../database/models/Aluno';
import { AlunoDto } from '../types/dtos';

// ── Helper ────────────────────────────────────────────────────────────────────

function alunoParaDto(a: Aluno): AlunoDto {
    return {
        id: a.id,
        nome: a.nome,
        turmaId: a.turmaId,
        matricula: '',
        faltasConsecutivasAtuais: a.faltasConsecutivasAtuais ?? 0,
        faltasNoTrimestre: a.faltasNoTrimestre ?? 0,
        totalFaltas: a.totalFaltas ?? 0,
        atrasosNoTrimestre: a.atrasosNoTrimestre ?? 0,
    };
}

// ── Service offline-first ─────────────────────────────────────────────────────
//
// TODAS as leituras e escritas vão ao WatermelonDB (SQLite local).
// A sincronização com a API acontece em background via watermelondbSync.ts.
// Isso garante que o app funcione mesmo sem Wi-Fi.

export const alunosService = {
    obterPorTurma: async (turmaId: string): Promise<AlunoDto[]> => {
        const collection = database.get<Aluno>('alunos');
        const alunos = await collection.query(Q.where('turma_id', turmaId)).fetch();
        return alunos.map(alunoParaDto);
    },

    criar: async (payload: { nome: string; matricula?: string; turmaId: string }): Promise<AlunoDto> => {
        const collection = database.get<Aluno>('alunos');
        let criado!: Aluno;
        await database.write(async () => {
            criado = await collection.create((a) => {
                a.nome = payload.nome;
                a.turmaId = payload.turmaId;
            });
        });
        return alunoParaDto(criado);
    },

    atualizar: async (id: string, payload: { id: string; nome: string; matricula?: string }): Promise<void> => {
        const collection = database.get<Aluno>('alunos');
        const aluno = await collection.find(id);
        await database.write(async () => {
            await aluno.update((a) => {
                a.nome = payload.nome;
            });
        });
    },

    // Histórico de presenças ainda precisa de rede — mantido para compatibilidade
    obterHistoricoPresencas: async (id: string): Promise<import('../types/dtos').HistoricoPresencaDto[]> => {
        try {
            const { api } = await import('./api');
            const response = await api.get(`/alunos/${id}/historico-presencas`);
            return response.data;
        } catch {
            return [];
        }
    }
};
