import React, { useState } from 'react';
import { View, StyleSheet, Alert, ScrollView } from 'react-native';
import { Text, Button, Surface, ActivityIndicator, Chip } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { TextInput } from 'react-native-paper';
import { AppNavigationProp } from '../../navigation/types';
import { AppHeader, EmptyState } from '../../components/ui';
import { theme } from '../../theme/colors';
import { api } from '../../services/api';
import { turmasService } from '../../services/turmasService';
import { alunosService } from '../../services/alunosService';
import { TurmaDto, AlunoDto } from '../../types/dtos';

interface RegistroHistorico {
    dataDaChamada: string;
    status: string;
}

const STATUS_CONFIG: Record<string, { label: string; icon: string; variant: 'success' | 'error' | 'warning' | 'info' }> = {
    Presente:         { label: 'Presente',    icon: 'check-circle',    variant: 'success' },
    Falta:            { label: 'Falta',       icon: 'close-circle',    variant: 'error' },
    Atraso:           { label: 'Atraso',      icon: 'clock-alert',     variant: 'warning' },
    FaltaJustificada: { label: 'Justificada', icon: 'file-document-check', variant: 'info' },
};

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
    const d = new Date(yyyy, mm - 1, dd);
    return isNaN(d.getTime()) ? null : d;
}

export function RelatorioPresencasScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    const [turmas, setTurmas] = useState<TurmaDto[]>([]);
    const [alunos, setAlunos] = useState<AlunoDto[]>([]);
    const [turmaSel, setTurmaSel] = useState<TurmaDto | null>(null);
    const [alunoSel, setAlunoSel] = useState<AlunoDto | null>(null);

    const hoje = new Date();
    const ha30 = new Date(); ha30.setDate(hoje.getDate() - 30);
    const [dataInicio, setDataInicio] = useState(formatarDateInput(ha30));
    const [dataFim, setDataFim] = useState(formatarDateInput(hoje));

    const [registros, setRegistros] = useState<RegistroHistorico[]>([]);
    const [loading, setLoading] = useState(false);
    const [buscou, setBuscou] = useState(false);

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
            const filtrado = resp.data.filter(r => {
                const dataLocal = new Date(r.dataDaChamada);
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

    const totalPresentes    = registros.filter(r => r.status === 'Presente').length;
    const totalFaltas       = registros.filter(r => r.status === 'Falta').length;
    const totalAtrasos      = registros.filter(r => r.status === 'Atraso').length;
    const totalJustificadas = registros.filter(r => r.status === 'FaltaJustificada').length;

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader title="Relatório de Presenças" onBack={() => navigation.goBack()} />

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
                                    {turmaSel.nome} — {turmaSel.turno}
                                </Text>
                            </View>
                            <Button
                                mode="text"
                                compact
                                onPress={() => { setTurmaSel(null); setAlunoSel(null); setRegistros([]); setBuscou(false); carregarTurmas(); }}
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
                            {etapa === 'turma' && turmas.map(t => (
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

                {/* Passo 2: Aluno */}
                {turmaSel && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={[styles.stepBadge, alunoSel && styles.stepBadgeDone]}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>
                                    {alunoSel ? '✓' : '2'}
                                </Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Aluno</Text>
                        </View>

                        {alunoSel ? (
                            <Surface style={styles.selectedBox} elevation={0}>
                                <View style={styles.selectedInfo}>
                                    <MaterialCommunityIcons name="account" size={20} color={theme.colors.primary} />
                                    <Text variant="bodyMedium" style={styles.selectedText}>{alunoSel.nome}</Text>
                                </View>
                                <Button
                                    mode="text"
                                    compact
                                    onPress={() => { setAlunoSel(null); setRegistros([]); setBuscou(false); setEtapa('aluno'); }}
                                >
                                    Alterar
                                </Button>
                            </Surface>
                        ) : carregandoAlunos ? (
                            <View style={styles.loadingRow}>
                                <ActivityIndicator size="small" />
                                <Text variant="bodySmall" style={styles.loadingText}>Carregando alunos...</Text>
                            </View>
                        ) : (
                            alunos.map(a => (
                                <Surface key={a.id} style={styles.listItem} elevation={0}>
                                    <Button
                                        mode="text"
                                        onPress={() => selecionarAluno(a)}
                                        contentStyle={styles.listItemContent}
                                        icon="chevron-right"
                                        style={styles.listItemButton}
                                    >
                                        {a.nome}
                                    </Button>
                                </Surface>
                            ))
                        )}
                    </Surface>
                )}

                {/* Passo 3: Período */}
                {alunoSel && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={styles.stepBadge}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>3</Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Período</Text>
                        </View>

                        <View style={styles.row}>
                            <TextInput
                                label="De"
                                value={dataInicio}
                                onChangeText={setDataInicio}
                                placeholder="DD/MM/AAAA"
                                keyboardType="numeric"
                                maxLength={10}
                                mode="outlined"
                                left={<TextInput.Icon icon="calendar-start" />}
                                style={styles.dateInput}
                            />
                            <TextInput
                                label="Até"
                                value={dataFim}
                                onChangeText={setDataFim}
                                placeholder="DD/MM/AAAA"
                                keyboardType="numeric"
                                maxLength={10}
                                mode="outlined"
                                left={<TextInput.Icon icon="calendar-end" />}
                                style={styles.dateInput}
                            />
                        </View>

                        <Button
                            mode="contained"
                            onPress={buscarRelatorio}
                            loading={loading}
                            disabled={loading}
                            icon="magnify"
                            style={styles.actionButton}
                            contentStyle={styles.actionButtonContent}
                        >
                            Buscar Histórico
                        </Button>
                    </Surface>
                )}

                {/* Resultado */}
                {buscou && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <MaterialCommunityIcons name="chart-bar" size={20} color={theme.colors.primary} />
                            <Text variant="labelLarge" style={styles.sectionTitle}>Resultado</Text>
                        </View>

                        {/* Resumo */}
                        <View style={styles.resumo}>
                            {[
                                { count: totalPresentes, label: 'Presentes', variant: 'success' as const },
                                { count: totalFaltas, label: 'Faltas', variant: 'error' as const },
                                { count: totalAtrasos, label: 'Atrasos', variant: 'warning' as const },
                                { count: totalJustificadas, label: 'Justif.', variant: 'info' as const },
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

                        {registros.length === 0 ? (
                            <EmptyState
                                icon="calendar-blank"
                                title="Nenhum registro encontrado neste período"
                            />
                        ) : (
                            registros.map((r, i) => {
                                const cfg = STATUS_CONFIG[r.status] ?? { label: r.status, icon: 'help-circle', variant: 'info' as const };
                                const colors = VARIANT_COLORS[cfg.variant];
                                return (
                                    <View key={i} style={styles.registro}>
                                        <Text variant="bodySmall" style={styles.registroData}>
                                            {formatarData(r.dataDaChamada)}
                                        </Text>
                                        <Chip
                                            compact
                                            icon={() => (
                                                <MaterialCommunityIcons name={cfg.icon as any} size={14} color={colors.color} />
                                            )}
                                            textStyle={{ fontSize: 11, fontWeight: '700', color: colors.color }}
                                            style={{ backgroundColor: colors.bg }}
                                        >
                                            {cfg.label}
                                        </Chip>
                                    </View>
                                );
                            })
                        )}
                    </Surface>
                )}
            </ScrollView>
        </SafeAreaView>
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
    row: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.md,
    },
    dateInput: {
        flex: 1,
        backgroundColor: theme.colors.surface,
    },
    resumo: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.md,
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
    registro: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingVertical: theme.spacing.sm,
        borderBottomWidth: 1,
        borderBottomColor: theme.colors.border,
    },
    registroData: {
        color: theme.colors.textPrimary,
        flex: 1,
    },
});
