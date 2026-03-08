import React, { useState } from 'react';
import {
    View, Text, StyleSheet, TouchableOpacity, FlatList,
    ActivityIndicator, Alert, ScrollView, TextInput,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { theme } from '../../theme/colors';
import { api } from '../../services/api';
import { turmasService } from '../../services/turmasService';
import { alunosService } from '../../services/alunosService';
import { TurmaDto, AlunoDto } from '../../types/dtos';

interface RegistroHistorico {
    dataDaChamada: string;
    status: string;
}

const STATUS_CONFIG: Record<string, { label: string; bg: string; color: string }> = {
    Presente:       { label: 'Presente',    bg: '#D1FAE5', color: '#065F46' },
    Falta:          { label: 'Falta',       bg: '#FEE2E2', color: '#991B1B' },
    Atraso:         { label: 'Atraso',      bg: '#FEF3C7', color: '#92400E' },
    FaltaJustificada: { label: 'Justificada', bg: '#E0E7FF', color: '#3730A3' },
};

function formatarData(isoUtc: string): string {
    try {
        return new Intl.DateTimeFormat('pt-BR', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit',
        }).format(new Date(isoUtc));
    } catch {
        return isoUtc;
    }
}

function formatarDateInput(d: Date): string {
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    return `${dd}/${mm}/${d.getFullYear()}`;
}

function parseDataInput(s: string): Date | null {
    const parts = s.split('/');
    if (parts.length !== 3) return null;
    const [dd, mm, yyyy] = parts.map(Number);
    if (isNaN(dd) || isNaN(mm) || isNaN(yyyy)) return null;
    const d = new Date(yyyy, mm - 1, dd); // data local, não UTC
    return isNaN(d.getTime()) ? null : d;
}

export function RelatorioPresencasScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    // Seleção
    const [turmas, setTurmas] = useState<TurmaDto[]>([]);
    const [alunos, setAlunos] = useState<AlunoDto[]>([]);
    const [turmaSel, setTurmaSel] = useState<TurmaDto | null>(null);
    const [alunoSel, setAlunoSel] = useState<AlunoDto | null>(null);

    // Período
    const hoje = new Date();
    const ha30 = new Date(); ha30.setDate(hoje.getDate() - 30);
    const [dataInicio, setDataInicio] = useState(formatarDateInput(ha30));
    const [dataFim, setDataFim] = useState(formatarDateInput(hoje));

    // Resultado
    const [registros, setRegistros] = useState<RegistroHistorico[]>([]);
    const [loading, setLoading] = useState(false);
    const [buscou, setBuscou] = useState(false);

    // Etapa atual: 'turma' | 'aluno' | 'periodo'
    const [etapa, setEtapa] = useState<'turma' | 'aluno' | 'periodo'>('turma');
    const [carregandoTurmas, setCarregandoTurmas] = useState(false);
    const [carregandoAlunos, setCarregandoAlunos] = useState(false);

    async function carregarTurmas() {
        setCarregandoTurmas(true);
        try {
            const data = await turmasService.obterTodas();
            setTurmas(data);
            setEtapa('turma');
        } catch {
            Alert.alert('Erro', 'Não foi possível carregar as turmas.');
        } finally {
            setCarregandoTurmas(false);
        }
    }

    async function selecionarTurma(turma: TurmaDto) {
        setTurmaSel(turma);
        setAlunoSel(null);
        setRegistros([]);
        setBuscou(false);
        setCarregandoAlunos(true);
        setEtapa('aluno');
        try {
            const data = await alunosService.obterPorTurma(turma.id);
            setAlunos(data);
        } catch {
            Alert.alert('Erro', 'Não foi possível carregar os alunos.');
        } finally {
            setCarregandoAlunos(false);
        }
    }

    function selecionarAluno(aluno: AlunoDto) {
        setAlunoSel(aluno);
        setRegistros([]);
        setBuscou(false);
        setEtapa('periodo');
    }

    async function buscarRelatorio() {
        if (!alunoSel) return;

        const inicio = parseDataInput(dataInicio);
        const fim = parseDataInput(dataFim);

        if (!inicio || !fim) {
            Alert.alert('Atenção', 'Informe datas válidas no formato DD/MM/AAAA.');
            return;
        }
        if (inicio > fim) {
            Alert.alert('Atenção', 'A data de início deve ser anterior à data de fim.');
            return;
        }

        const dias = Math.ceil((fim.getTime() - inicio.getTime()) / (1000 * 60 * 60 * 24)) + 1;
        if (dias > 366) {
            Alert.alert('Atenção', 'O período máximo é de 1 ano.');
            return;
        }

        setLoading(true);
        setBuscou(false);
        try {
            const resp = await api.get<RegistroHistorico[]>(
                `/alunos/${alunoSel.id}/historico-presencas`,
                { params: { dias } }
            );
            // Filtra pelo período exato usando timestamps locais
            // inicio/fim são datas locais (meia-noite local), comparamos com a data local do registro
            const filtrado = resp.data.filter(r => {
                const dataLocal = new Date(r.dataDaChamada);
                // Compara apenas o dia/mes/ano local, ignorando hora
                const dLocal = new Date(dataLocal.getFullYear(), dataLocal.getMonth(), dataLocal.getDate());
                const dInicio = new Date(inicio.getFullYear(), inicio.getMonth(), inicio.getDate());
                const dFim = new Date(fim.getFullYear(), fim.getMonth(), fim.getDate());
                return dLocal >= dInicio && dLocal <= dFim;
            });
            setRegistros(filtrado);
            setBuscou(true);
        } catch {
            Alert.alert('Erro', 'Não foi possível carregar o histórico. Verifique a conexão.');
        } finally {
            setLoading(false);
        }
    }

    const totalPresentes   = registros.filter(r => r.status === 'Presente').length;
    const totalFaltas      = registros.filter(r => r.status === 'Falta').length;
    const totalAtrasos     = registros.filter(r => r.status === 'Atraso').length;
    const totalJustificadas = registros.filter(r => r.status === 'FaltaJustificada').length;

    return (
        <SafeAreaView style={styles.container}>
            {/* Header */}
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Relatório de Presenças</Text>
            </View>

            <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">

                {/* Passo 1: Turma */}
                <View style={styles.section}>
                    <Text style={styles.sectionTitle}>1. Turma</Text>
                    {turmaSel ? (
                        <TouchableOpacity style={styles.selectedBox} onPress={() => { setTurmaSel(null); setAlunoSel(null); setRegistros([]); setBuscou(false); carregarTurmas(); }}>
                            <Text style={styles.selectedText}>{turmaSel.nome} — {turmaSel.turno}</Text>
                            <Text style={styles.changeText}>Alterar</Text>
                        </TouchableOpacity>
                    ) : (
                        <>
                            <TouchableOpacity style={styles.loadButton} onPress={carregarTurmas} disabled={carregandoTurmas}>
                                {carregandoTurmas
                                    ? <ActivityIndicator color={theme.colors.surface} />
                                    : <Text style={styles.loadButtonText}>Selecionar Turma</Text>}
                            </TouchableOpacity>
                            {etapa === 'turma' && turmas.length > 0 && turmas.map(t => (
                                <TouchableOpacity key={t.id} style={styles.listItem} onPress={() => selecionarTurma(t)}>
                                    <Text style={styles.listItemText}>{t.nome}</Text>
                                    <Text style={styles.listItemSub}>{t.turno} — {t.anoLetivo}</Text>
                                </TouchableOpacity>
                            ))}
                        </>
                    )}
                </View>

                {/* Passo 2: Aluno */}
                {turmaSel && (
                    <View style={styles.section}>
                        <Text style={styles.sectionTitle}>2. Aluno</Text>
                        {alunoSel ? (
                            <TouchableOpacity style={styles.selectedBox} onPress={() => { setAlunoSel(null); setRegistros([]); setBuscou(false); setEtapa('aluno'); }}>
                                <Text style={styles.selectedText}>{alunoSel.nome}</Text>
                                <Text style={styles.changeText}>Alterar</Text>
                            </TouchableOpacity>
                        ) : carregandoAlunos ? (
                            <ActivityIndicator color={theme.colors.primary} style={{ marginTop: 8 }} />
                        ) : (
                            alunos.map(a => (
                                <TouchableOpacity key={a.id} style={styles.listItem} onPress={() => selecionarAluno(a)}>
                                    <Text style={styles.listItemText}>{a.nome}</Text>
                                </TouchableOpacity>
                            ))
                        )}
                    </View>
                )}

                {/* Passo 3: Período */}
                {alunoSel && (
                    <View style={styles.section}>
                        <Text style={styles.sectionTitle}>3. Período</Text>
                        <View style={styles.row}>
                            <View style={styles.inputGroup}>
                                <Text style={styles.inputLabel}>De</Text>
                                <TextInput
                                    style={styles.input}
                                    value={dataInicio}
                                    onChangeText={setDataInicio}
                                    placeholder="DD/MM/AAAA"
                                    keyboardType="numeric"
                                    maxLength={10}
                                />
                            </View>
                            <View style={styles.inputGroup}>
                                <Text style={styles.inputLabel}>Até</Text>
                                <TextInput
                                    style={styles.input}
                                    value={dataFim}
                                    onChangeText={setDataFim}
                                    placeholder="DD/MM/AAAA"
                                    keyboardType="numeric"
                                    maxLength={10}
                                />
                            </View>
                        </View>
                        <TouchableOpacity style={styles.searchButton} onPress={buscarRelatorio} disabled={loading}>
                            {loading
                                ? <ActivityIndicator color={theme.colors.surface} />
                                : <Text style={styles.searchButtonText}>Buscar Histórico</Text>}
                        </TouchableOpacity>
                    </View>
                )}

                {/* Resultado */}
                {buscou && (
                    <View style={styles.section}>
                        <Text style={styles.sectionTitle}>Resultado</Text>

                        {/* Resumo */}
                        <View style={styles.resumo}>
                            <View style={[styles.resumoCard, { backgroundColor: '#D1FAE5' }]}>
                                <Text style={[styles.resumoNum, { color: '#065F46' }]}>{totalPresentes}</Text>
                                <Text style={[styles.resumoLabel, { color: '#065F46' }]}>Presentes</Text>
                            </View>
                            <View style={[styles.resumoCard, { backgroundColor: '#FEE2E2' }]}>
                                <Text style={[styles.resumoNum, { color: '#991B1B' }]}>{totalFaltas}</Text>
                                <Text style={[styles.resumoLabel, { color: '#991B1B' }]}>Faltas</Text>
                            </View>
                            <View style={[styles.resumoCard, { backgroundColor: '#FEF3C7' }]}>
                                <Text style={[styles.resumoNum, { color: '#92400E' }]}>{totalAtrasos}</Text>
                                <Text style={[styles.resumoLabel, { color: '#92400E' }]}>Atrasos</Text>
                            </View>
                            <View style={[styles.resumoCard, { backgroundColor: '#E0E7FF' }]}>
                                <Text style={[styles.resumoNum, { color: '#3730A3' }]}>{totalJustificadas}</Text>
                                <Text style={[styles.resumoLabel, { color: '#3730A3' }]}>Justif.</Text>
                            </View>
                        </View>

                        {registros.length === 0 ? (
                            <Text style={styles.emptyText}>Nenhum registro encontrado neste período.</Text>
                        ) : (
                            registros.map((r, i) => {
                                const cfg = STATUS_CONFIG[r.status] ?? { label: r.status, bg: '#F3F4F6', color: '#374151' };
                                return (
                                    <View key={i} style={styles.registro}>
                                        <Text style={styles.registroData}>{formatarData(r.dataDaChamada)}</Text>
                                        <View style={[styles.badge, { backgroundColor: cfg.bg }]}>
                                            <Text style={[styles.badgeText, { color: cfg.color }]}>{cfg.label}</Text>
                                        </View>
                                    </View>
                                );
                            })
                        )}
                    </View>
                )}
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    header: { flexDirection: 'row', alignItems: 'center', padding: 20, backgroundColor: theme.colors.surface, elevation: 2 },
    backButton: { marginRight: 16 },
    backText: { fontSize: 16, color: theme.colors.primary, fontWeight: '600' },
    headerTitle: { fontSize: 18, fontWeight: 'bold', color: theme.colors.textPrimary },
    content: { padding: 16, paddingBottom: 40 },
    section: { backgroundColor: theme.colors.surface, borderRadius: 12, padding: 16, marginBottom: 16, elevation: 1 },
    sectionTitle: { fontSize: 14, fontWeight: 'bold', color: theme.colors.textSecondary, marginBottom: 12, textTransform: 'uppercase', letterSpacing: 0.5 },
    loadButton: { backgroundColor: theme.colors.primary, padding: 14, borderRadius: 10, alignItems: 'center' },
    loadButtonText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 15 },
    listItem: { paddingVertical: 12, paddingHorizontal: 4, borderBottomWidth: 1, borderBottomColor: theme.colors.border },
    listItemText: { fontSize: 15, color: theme.colors.textPrimary, fontWeight: '500' },
    listItemSub: { fontSize: 12, color: theme.colors.textSecondary, marginTop: 2 },
    selectedBox: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', backgroundColor: theme.colors.background, padding: 12, borderRadius: 8, borderWidth: 1, borderColor: theme.colors.primary },
    selectedText: { fontSize: 15, color: theme.colors.textPrimary, fontWeight: '500', flex: 1 },
    changeText: { fontSize: 13, color: theme.colors.primary, fontWeight: '600' },
    row: { flexDirection: 'row', gap: 12 },
    inputGroup: { flex: 1 },
    inputLabel: { fontSize: 13, color: theme.colors.textSecondary, marginBottom: 4 },
    input: { backgroundColor: theme.colors.background, borderWidth: 1, borderColor: theme.colors.border, borderRadius: 8, padding: 12, fontSize: 15, color: theme.colors.textPrimary },
    searchButton: { backgroundColor: theme.colors.secondary, padding: 14, borderRadius: 10, alignItems: 'center', marginTop: 12 },
    searchButtonText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 15 },
    resumo: { flexDirection: 'row', gap: 8, marginBottom: 16 },
    resumoCard: { flex: 1, borderRadius: 10, padding: 10, alignItems: 'center' },
    resumoNum: { fontSize: 22, fontWeight: 'bold' },
    resumoLabel: { fontSize: 11, fontWeight: '600', marginTop: 2 },
    registro: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: 10, borderBottomWidth: 1, borderBottomColor: theme.colors.border },
    registroData: { fontSize: 14, color: theme.colors.textPrimary, flex: 1 },
    badge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 12 },
    badgeText: { fontSize: 12, fontWeight: 'bold' },
    emptyText: { textAlign: 'center', color: theme.colors.textSecondary, marginTop: 16, fontSize: 14 },
});
