import database from '../database';
import Turma from '../database/models/Turma';
import { TurmaDto } from '../types/dtos';

// ── Helpers ──────────────────────────────────────────────────────────────────

function turmaParaDto(t: Turma): TurmaDto {
    return {
        id: t.id,
        nome: t.nome,
        turno: t.turno,
        anoLetivo: t.anoLetivo,
    };
}

// ── Service offline-first ─────────────────────────────────────────────────────
//
// TODAS as leituras e escritas vão ao WatermelonDB (SQLite local).
// A sincronização com a API acontece em background via watermelondbSync.ts.
// Isso garante que o app funcione mesmo sem Wi-Fi.

export const turmasService = {
    obterTodas: async (): Promise<TurmaDto[]> => {
        const collection = database.get<Turma>('turmas');
        const turmas = await collection.query().fetch();
        return turmas.map(turmaParaDto);
    },

    criar: async (data: Omit<TurmaDto, 'id'>): Promise<TurmaDto> => {
        const collection = database.get<Turma>('turmas');
        let criada!: Turma;
        await database.write(async () => {
            criada = await collection.create((t) => {
                t.nome = data.nome;
                t.turno = data.turno;
                t.anoLetivo = data.anoLetivo ?? new Date().getFullYear();
            });
        });
        return turmaParaDto(criada);
    },

    atualizar: async (id: string, data: Omit<TurmaDto, 'id'>): Promise<void> => {
        const collection = database.get<Turma>('turmas');
        const turma = await collection.find(id);
        await database.write(async () => {
            await turma.update((t) => {
                t.nome = data.nome;
                t.turno = data.turno;
                t.anoLetivo = data.anoLetivo ?? t.anoLetivo;
            });
        });
    },
};
