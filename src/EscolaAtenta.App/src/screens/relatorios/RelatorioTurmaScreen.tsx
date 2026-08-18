import React, { useState, useEffect, useCallback } from 'react';
import { View, StyleSheet, Alert, ScrollView } from 'react-native';
import { Text, Button, Surface, ActivityIndicator, Chip, Divider } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { AppHeader, EmptyState } from '../../components/ui';
import { theme } from '../../theme/colors';
import { api } from '../../services/api';
import { turmasService } from '../../services/turmasService';
import {
    TurmaDto,
    RelatorioTurmaDto,
    RelatorioTurmaAlunoDto,
    PeriodosLetivosDisponiveisDto,
    PeriodoLetivoDisponivelDto,
} from '../../types/dtos';

const VARIANT_COLORS: Record<string, { bg: string; color: string }> = {
    success: { bg: theme.colors.successLight, color: theme.colors.success },
    error:   { bg: theme.colors.errorLight,   color: theme.colors.error },
    warning: { bg: theme.colors.warningLight,  color: theme.colors.warning },
    info:    { bg: theme.colors.infoLight,     color: theme.colors.info },
};

function formatarData(isoUtc: string): string {
    try {
        return new Intl.DateTimeFormat('pt-BR', {
            day: '2-digit', month: '2-digit', year: 'numeric',
        }).format(new Date(isoUtc));
    } catch {
        return isoUtc;
    }
}

export function RelatorioTurmaScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    const [turmas, setTurmas] = useState<TurmaDto[]>([]);
    const [turmaSel, setTurmaSel] = useState<TurmaDto | null>(null);
    const [anoLetivo, setAnoLetivo] = useState<number>(new Date().getFullYear());

    const [periodosDisponiveis, setPeriodosDisponiveis] = useState<PeriodoLetivoDisponivelDto[]>([]);
    const [periodoSel, setPeriodoSel] = useState<PeriodoLetivoDisponivelDto | null>(null);
    const [tipoPeriodo, setTipoPeriodo] = useState<string>('');
    const [mostraSeletorPeriodo, setMostraSeletorPeriodo] = useState(false);

    const [relatorio, setRelatorio] = useState<RelatorioTurmaDto | null>(null);
    const [carregandoTurmas, setCarregandoTurmas] = useState(false);
    const [carregandoPeriodos, setCarregandoPeriodos] = useState(false);
    const [buscando, setBuscando] = useState(false);

    async function carregarTurmas() {
        setCarregandoTurmas(true);
        try {
            const data = await turmasService.obterTodas();
            setTurmas(data.sort((a, b) => a.nome.localeCompare(b.nome)));
        } catch {
            Alert.alert('Erro', 'Não foi possível carregar as turmas.');
        } finally {
            setCarregandoTurmas(false);
        }
    }

    async function carregarPeriodosDisponiveis(turma: TurmaDto, ano: number) {
        setCarregandoPeriodos(true);
        try {
            const resp = await api.get<PeriodosLetivosDisponiveisDto>(
                '/configuracao-escola/periodos-disponiveis',
                { params: { anoLetivo: ano } }
            );
            const periodos = resp.data.periodos;
            setTipoPeriodo(resp.data.tipoPeriodoLetivo);
            setPeriodosDisponiveis(periodos);

            if (periodos.length > 1) {
                setMostraSeletorPeriodo(true);
                // Pré-seleciona o último período iniciado (mais próximo do atual).
                setPeriodoSel(periodos[periodos.length - 1]);
            } else {
                setMostraSeletorPeriodo(false);
                setPeriodoSel(periodos.length === 1 ? periodos[0] : null);
            }
        } catch {
            // Fallback silencioso: oculta o seletor e permite buscar sem período.
            setMostraSeletorPeriodo(false);
            setPeriodoSel(null);
            setPeriodosDisponiveis([]);
        } finally {
            setCarregandoPeriodos(false);
        }
    }

    function selecionarTurma(turma: TurmaDto) {
        setTurmaSel(turma);
        setAnoLetivo(turma.anoLetivo ?? new Date().getFullYear());
        setRelatorio(null);
        carregarPeriodosDisponiveis(turma, turma.anoLetivo ?? new Date().getFullYear());
    }

    async function buscarRelatorio() {
        if (!turmaSel) return;

        setBuscando(true);
        setRelatorio(null);
        try {
            const params: Record<string, any> = { anoLetivo };
            if (periodoSel) {
                params.periodoLetivo = periodoSel.numero;
            }
            const resp = await api.get<RelatorioTurmaDto>(
                `/turmas/${turmaSel.id}/relatorio`,
                { params }
            );
            setRelatorio(resp.data);
        } catch {
            Alert.alert('Erro', 'Não foi possível carregar o relatório. Verifique a conexão.');
        } finally {
            setBuscando(false);
        }
    }

    useFocusEffect(
        useCallback(() => {
            carregarTurmas();
        }, [])
    );

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader title="Relatório por Turma" onBack={() => navigation.goBack()} />

            <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
                {/* Passo 1: Turma */}
                <Surface style={styles.section} elevation={1}>
                    <View style={styles.sectionHeader}>
                        <View style={[styles.stepBadge, turmaSel && styles.stepBadgeDone]}>
                            <Text variant="labelSmall" style={styles.stepBadgeText}>
                                {turmaSel ? '✓' : '1'}
                            </Text>
                        </View>
                        <Text variant="labelLarge" style={styles.sectionTitle}>Turma</Text>
                    </View>

                    {turmaSel ? (
                        <Surface style={styles.selectedBox} elevation={0}>
                            <View style={styles.selectedInfo}>
                                <MaterialCommunityIcons name="google-classroom" size={20} color={theme.colors.primary} />
                                <Text variant="bodyMedium" style={styles.selectedText}>
                                    {turmaSel.nome} — {turmaSel.turno} — {turmaSel.anoLetivo}
                                </Text>
                            </View>
                            <Button
                                mode="text"
                                compact
                                onPress={() => {
                                    setTurmaSel(null);
                                    setRelatorio(null);
                                    setPeriodoSel(null);
                                    setMostraSeletorPeriodo(false);
                                }}
                            >
                                Alterar
                            </Button>
                        </Surface>
                    ) : (
                        <>
                            <Button
                                mode="contained"
                                onPress={carregarTurmas}
                                loading={carregandoTurmas}
                                disabled={carregandoTurmas}
                                icon="format-list-bulleted"
                                style={styles.actionButton}
                            >
                                Selecionar Turma
                            </Button>
                            {carregandoTurmas && (
                                <View style={styles.loadingRow}>
                                    <ActivityIndicator size="small" />
                                    <Text variant="bodySmall" style={styles.loadingText}>Carregando turmas...</Text>
                                </View>
                            )}
                            {!carregandoTurmas && turmas.map(t => (
                                <Surface key={t.id} style={styles.listItem} elevation={0}>
                                    <Button
                                        mode="text"
                                        onPress={() => selecionarTurma(t)}
                                        contentStyle={styles.listItemContent}
                                        icon="chevron-right"
                                        style={styles.listItemButton}
                                    >
                                        {`${t.nome} — ${t.turno} — ${t.anoLetivo}`}
                                    </Button>
                                </Surface>
                            ))}
                        </>
                    )}
                </Surface>

                {/* Passo 2: Ano letivo e período */}
                {turmaSel && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={styles.stepBadge}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>2</Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Ano e Período Letivo</Text>
                        </View>

                        <View style={styles.infoRow}>
                            <MaterialCommunityIcons name="calendar" size={18} color={theme.colors.primary} />
                            <Text variant="bodyMedium">
                                Ano letivo: <Text style={styles.bold}>{anoLetivo}</Text>
                            </Text>
                        </View>

                        {carregandoPeriodos ? (
                            <View style={styles.loadingRow}>
                                <ActivityIndicator size="small" />
                                <Text variant="bodySmall" style={styles.loadingText}>Carregando períodos...</Text>
                            </View>
                        ) : mostraSeletorPeriodo ? (
                            <>
                                <Text variant="bodySmall" style={styles.helperText}>
                                    Selecione o {tipoPeriodo.toLowerCase()} que deseja visualizar:
                                </Text>
                                <View style={styles.chipRow}>
                                    {periodosDisponiveis.map(p => (
                                        <Chip
                                            key={p.numero}
                                            selected={periodoSel?.numero === p.numero}
                                            onPress={() => setPeriodoSel(p)}
                                            style={styles.chip}
                                            showSelectedOverlay
                                        >
                                            {p.descricao}
                                        </Chip>
                                    ))}
                                </View>
                            </>
                        ) : periodoSel ? (
                            <View style={styles.infoRow}>
                                <MaterialCommunityIcons name="clock-outline" size={18} color={theme.colors.success} />
                                <Text variant="bodyMedium">
                                    Período atual: <Text style={styles.bold}>{periodoSel.descricao}</Text>
                                </Text>
                            </View>
                        ) : null}

                        <Button
                            mode="contained"
                            onPress={buscarRelatorio}
                            loading={buscando}
                            disabled={buscando}
                            icon="magnify"
                            style={[styles.actionButton, { marginTop: theme.spacing.md }]}
                            contentStyle={styles.actionButtonContent}
                        >
                            Buscar Relatório
                        </Button>
                    </Surface>
                )}

                {/* Resultado */}
                {relatorio && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <MaterialCommunityIcons name="chart-bar" size={20} color={theme.colors.primary} />
                            <Text variant="labelLarge" style={styles.sectionTitle}>Resultado</Text>
                        </View>

                        <Text variant="bodySmall" style={styles.periodoInfo}>
                            Período: {formatarData(relatorio.periodoInicio)} até {formatarData(relatorio.periodoFim)}
                        </Text>

                        {/* Resumo */}
                        <View style={styles.resumo}>
                            {[
                                { count: relatorio.resumo.totalAlunos, label: 'Alunos', variant: 'info' as const },
                                { count: relatorio.resumo.totalPresentes, label: 'Presentes', variant: 'success' as const },
                                { count: relatorio.resumo.totalFaltas, label: 'Faltas', variant: 'error' as const },
                                { count: relatorio.resumo.totalFaltasJustificadas, label: 'Justif.', variant: 'warning' as const },
                            ].map(item => {
                                const colors = VARIANT_COLORS[item.variant];
                                return (
                                    <Surface key={item.label} style={[styles.resumoCard, { backgroundColor: colors.bg }]} elevation={0}>
                                        <Text variant="headlineSmall" style={[styles.resumoNum, { color: colors.color }]}>
                                            {item.count}
                                        </Text>
                                        <Text variant="labelSmall" style={[styles.resumoLabel, { color: colors.color }]}>
                                            {item.label}
                                        </Text>
                                    </Surface>
                                );
                            })}
                        </View>

                        <View style={[styles.resumo, { marginTop: theme.spacing.sm }]}>
                            {[
                                { count: relatorio.resumo.totalAtrasos, label: 'Atrasos', variant: 'warning' as const },
                                { count: `${relatorio.resumo.percentualPresencaTurma.toFixed(0)}%`, label: 'Presença', variant: 'success' as const },
                            ].map(item => {
                                const colors = VARIANT_COLORS[item.variant];
                                return (
                                    <Surface key={item.label} style={[styles.resumoCard, { backgroundColor: colors.bg }]} elevation={0}>
                                        <Text variant="headlineSmall" style={[styles.resumoNum, { color: colors.color }]}>
                                            {item.count}
                                        </Text>
                                        <Text variant="labelSmall" style={[styles.resumoLabel, { color: colors.color }]}>
                                            {item.label}
                                        </Text>
                                    </Surface>
                                );
                            })}
                        </View>

                        <Divider style={styles.divider} />

                        {relatorio.alunos.length === 0 ? (
                            <EmptyState
                                icon="account-group-outline"
                                title="Nenhum aluno encontrado"
                                subtitle="Não há alunos vinculados a esta turma no período selecionado."
                            />
                        ) : (
                            relatorio.alunos.map((a, idx) => (
                                <AlunoRow key={a.alunoId} aluno={a} index={idx} />
                            ))
                        )}
                    </Surface>
                )}
            </ScrollView>
        </SafeAreaView>
    );
}

function AlunoRow({ aluno, index }: { aluno: RelatorioTurmaAlunoDto; index: number }) {
    return (
        <Surface style={[styles.alunoCard, index % 2 === 0 ? styles.alunoCardEven : null]} elevation={0}>
            <View style={styles.alunoHeader}>
                <Text variant="bodyMedium" style={styles.alunoNome}>{aluno.nomeAluno}</Text>
                <Chip
                    compact
                    style={{ backgroundColor: theme.colors.successLight }}
                    textStyle={{ color: theme.colors.success, fontSize: 11, fontWeight: '700' }}
                >
                    {aluno.percentualPresenca.toFixed(0)}%
                </Chip>
            </View>
            {aluno.matricula && (
                <Text variant="bodySmall" style={styles.matricula}>Matrícula: {aluno.matricula}</Text>
            )}
            <View style={styles.alunoStats}>
                <Stat value={aluno.presentes} label="P" color={theme.colors.success} />
                <Stat value={aluno.faltas} label="F" color={theme.colors.error} />
                <Stat value={aluno.faltasJustificadas} label="FJ" color={theme.colors.warning} />
                <Stat value={aluno.atrasos} label="A" color={theme.colors.info} />
            </View>
        </Surface>
    );
}

function Stat({ value, label, color }: { value: number; label: string; color: string }) {
    return (
        <View style={styles.stat}>
            <Text variant="labelLarge" style={[styles.statValue, { color }]}>{value}</Text>
            <Text variant="labelSmall" style={styles.statLabel}>{label}</Text>
        </View>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    content: { padding: theme.spacing.md, paddingBottom: theme.spacing.xxl },
    section: {
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.md,
        padding: theme.spacing.md,
        marginBottom: theme.spacing.md,
    },
    sectionHeader: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.md,
    },
    sectionTitle: {
        color: theme.colors.textSecondary,
        textTransform: 'uppercase',
        letterSpacing: 0.5,
    },
    stepBadge: {
        width: 24,
        height: 24,
        borderRadius: 12,
        backgroundColor: theme.colors.primary,
        alignItems: 'center',
        justifyContent: 'center',
    },
    stepBadgeDone: {
        backgroundColor: theme.colors.success,
    },
    stepBadgeText: {
        color: theme.colors.surface,
        fontWeight: 'bold',
        fontSize: 12,
    },
    selectedBox: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        backgroundColor: theme.colors.primaryLight,
        padding: theme.spacing.sm,
        borderRadius: theme.borderRadius.sm,
        borderWidth: 1,
        borderColor: theme.colors.primary,
    },
    selectedInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.sm,
        flex: 1,
    },
    selectedText: {
        color: theme.colors.textPrimary,
        fontWeight: '500',
    },
    actionButton: {
        borderRadius: theme.borderRadius.sm,
    },
    actionButtonContent: {
        paddingVertical: theme.spacing.xs,
    },
    listItem: {
        backgroundColor: theme.colors.background,
        borderRadius: theme.borderRadius.sm,
        marginBottom: 4,
    },
    listItemButton: {
        borderRadius: theme.borderRadius.sm,
    },
    listItemContent: {
        justifyContent: 'flex-start',
    },
    loadingRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: theme.spacing.sm,
        paddingVertical: theme.spacing.md,
    },
    loadingText: {
        color: theme.colors.textSecondary,
    },
    infoRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.sm,
    },
    helperText: {
        color: theme.colors.textSecondary,
        marginBottom: theme.spacing.sm,
    },
    bold: {
        fontWeight: 'bold',
    },
    chipRow: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: theme.spacing.sm,
    },
    chip: {
        marginBottom: theme.spacing.xs,
    },
    periodoInfo: {
        color: theme.colors.textSecondary,
        marginBottom: theme.spacing.md,
    },
    resumo: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
    },
    resumoCard: {
        flex: 1,
        borderRadius: theme.borderRadius.sm,
        padding: theme.spacing.sm,
        alignItems: 'center',
    },
    resumoNum: {
        fontWeight: 'bold',
    },
    resumoLabel: {
        fontWeight: '600',
        marginTop: 2,
    },
    divider: {
        marginVertical: theme.spacing.md,
    },
    alunoCard: {
        padding: theme.spacing.sm,
        borderRadius: theme.borderRadius.sm,
        marginBottom: theme.spacing.xs,
    },
    alunoCardEven: {
        backgroundColor: theme.colors.background,
    },
    alunoHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 2,
    },
    alunoNome: {
        fontWeight: '600',
        color: theme.colors.textPrimary,
        flex: 1,
    },
    matricula: {
        color: theme.colors.textSecondary,
        marginBottom: theme.spacing.xs,
    },
    alunoStats: {
        flexDirection: 'row',
        gap: theme.spacing.md,
    },
    stat: {
        alignItems: 'center',
        minWidth: 32,
    },
    statValue: {
        fontWeight: 'bold',
    },
    statLabel: {
        color: theme.colors.textSecondary,
    },
});
