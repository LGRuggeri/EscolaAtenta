import React, { useState, useEffect, useCallback } from 'react';
import { View, StyleSheet, Alert, ScrollView } from 'react-native';
import { Text, Button, Surface, ActivityIndicator, Divider, TextInput, Chip } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { AppHeader, EmptyState } from '../../components/ui';
import { theme } from '../../theme/colors';
import { api } from '../../services/api';
import { turmasService } from '../../services/turmasService';
import { TurmaDto, RelatorioTurmaDto, RelatorioTurmaAlunoDto } from '../../types/dtos';

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

function dataHojeLocal(): Date {
    const agora = new Date();
    return new Date(agora.getFullYear(), agora.getMonth(), agora.getDate());
}

function paraIsoLocal(d: Date): string {
    const ano = d.getFullYear();
    const mes = String(d.getMonth() + 1).padStart(2, '0');
    const dia = String(d.getDate()).padStart(2, '0');
    return `${ano}-${mes}-${dia}`;
}

function paraInputBr(d: Date): string {
    const dia = String(d.getDate()).padStart(2, '0');
    const mes = String(d.getMonth() + 1).padStart(2, '0');
    const ano = d.getFullYear();
    return `${dia}/${mes}/${ano}`;
}

function parseDataBr(texto: string): Date | null {
    const limpo = texto.replace(/\D/g, '');
    if (limpo.length !== 8) return null;

    const dia = parseInt(limpo.substring(0, 2), 10);
    const mes = parseInt(limpo.substring(2, 4), 10) - 1;
    const ano = parseInt(limpo.substring(4, 8), 10);

    const data = new Date(ano, mes, dia);
    if (
        data.getFullYear() !== ano ||
        data.getMonth() !== mes ||
        data.getDate() !== dia ||
        ano < 2000 ||
        ano > 2100
    ) {
        return null;
    }
    return data;
}

export function RelatorioTurmaScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    const [turmas, setTurmas] = useState<TurmaDto[]>([]);
    const [turmaSel, setTurmaSel] = useState<TurmaDto | null>(null);

    const [dataInicio, setDataInicio] = useState<Date>(dataHojeLocal());
    const [dataFim, setDataFim] = useState<Date>(dataHojeLocal());
    const [inputInicio, setInputInicio] = useState(paraInputBr(dataHojeLocal()));
    const [inputFim, setInputFim] = useState(paraInputBr(dataHojeLocal()));
    const [erroInicio, setErroInicio] = useState<string | undefined>(undefined);
    const [erroFim, setErroFim] = useState<string | undefined>(undefined);

    const [relatorio, setRelatorio] = useState<RelatorioTurmaDto | null>(null);
    const [carregandoTurmas, setCarregandoTurmas] = useState(false);
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

    function selecionarTurma(turma: TurmaDto) {
        setTurmaSel(turma);
        setRelatorio(null);
    }

    function aplicarDataInicio(texto: string) {
        setInputInicio(texto);
        const data = parseDataBr(texto);
        if (data) {
            setDataInicio(data);
            setErroInicio(undefined);
        } else if (texto.length === 10) {
            setErroInicio('Data inválida');
        }
    }

    function aplicarDataFim(texto: string) {
        setInputFim(texto);
        const data = parseDataBr(texto);
        if (data) {
            setDataFim(data);
            setErroFim(undefined);
        } else if (texto.length === 10) {
            setErroFim('Data inválida');
        }
    }

    function definirPreset(dias: number) {
        const fim = dataHojeLocal();
        const inicio = new Date(fim);
        inicio.setDate(fim.getDate() - dias);
        setDataInicio(inicio);
        setDataFim(fim);
        setInputInicio(paraInputBr(inicio));
        setInputFim(paraInputBr(fim));
        setRelatorio(null);
    }

    async function buscarRelatorio() {
        if (!turmaSel) return;

        const inicio = parseDataBr(inputInicio);
        const fim = parseDataBr(inputFim);

        if (!inicio || !fim) {
            if (!inicio) setErroInicio('Data inválida');
            if (!fim) setErroFim('Data inválida');
            Alert.alert('Datas inválidas', 'Informe uma data de início e fim válidas no formato DD/MM/AAAA.');
            return;
        }

        if (inicio > fim) {
            Alert.alert('Datas inválidas', 'A data de início deve ser anterior ou igual à data de fim.');
            return;
        }

        const turmaIdApi = turmaSel.serverId || turmaSel.id;
        if (!turmaIdApi) {
            Alert.alert('Erro', 'Turma sem identificador para consulta.');
            return;
        }

        setBuscando(true);
        setRelatorio(null);
        try {
            const resp = await api.get<RelatorioTurmaDto>(
                `/turmas/${turmaIdApi}/relatorio`,
                {
                    params: {
                        dataInicio: paraIsoLocal(inicio),
                        dataFim: paraIsoLocal(fim),
                    },
                }
            );
            setRelatorio(resp.data);
        } catch (err: any) {
            const msg = err.response?.data?.erro
                || err.response?.data?.message
                || 'Não foi possível carregar o relatório. Verifique a conexão.';
            Alert.alert('Erro', msg);
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

                {/* Passo 2: Intervalo de datas */}
                {turmaSel && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={styles.stepBadge}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>2</Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Período</Text>
                        </View>

                        <View style={styles.presetRow}>
                            <Button mode="outlined" compact onPress={() => definirPreset(0)} style={styles.presetButton}>Hoje</Button>
                            <Button mode="outlined" compact onPress={() => definirPreset(6)} style={styles.presetButton}>7 dias</Button>
                            <Button mode="outlined" compact onPress={() => definirPreset(29)} style={styles.presetButton}>30 dias</Button>
                        </View>

                        <View style={styles.dateInputsRow}>
                            <TextInput
                                label="Data início (DD/MM/AAAA)"
                                value={inputInicio}
                                onChangeText={aplicarDataInicio}
                                onBlur={() => {
                                    const data = parseDataBr(inputInicio);
                                    if (data) {
                                        setInputInicio(paraInputBr(data));
                                        setErroInicio(undefined);
                                    } else {
                                        setErroInicio('Data inválida');
                                    }
                                }}
                                keyboardType="numeric"
                                mode="outlined"
                                style={styles.dateInput}
                                maxLength={10}
                                error={!!erroInicio}
                            />
                            <TextInput
                                label="Data fim (DD/MM/AAAA)"
                                value={inputFim}
                                onChangeText={aplicarDataFim}
                                onBlur={() => {
                                    const data = parseDataBr(inputFim);
                                    if (data) {
                                        setInputFim(paraInputBr(data));
                                        setErroFim(undefined);
                                    } else {
                                        setErroFim('Data inválida');
                                    }
                                }}
                                keyboardType="numeric"
                                mode="outlined"
                                style={styles.dateInput}
                                maxLength={10}
                                error={!!erroFim}
                            />
                        </View>

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
    presetRow: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.md,
    },
    presetButton: {
        flex: 1,
    },
    dateInputsRow: {
        flexDirection: 'row',
        gap: theme.spacing.md,
    },
    dateInput: {
        flex: 1,
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
