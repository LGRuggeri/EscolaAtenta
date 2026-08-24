import React, { useState, useCallback } from 'react';
import { View, StyleSheet, Alert, ScrollView, Pressable } from 'react-native';
import { Text, Button, Surface, ActivityIndicator, TextInput, Checkbox } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { AppHeader, EmptyState } from '../../components/ui';
import { theme } from '../../theme/colors';
import { api } from '../../services/api';
import { turmasService } from '../../services/turmasService';
import { useAuth } from '../../hooks/useAuth';
import { TurmaDto, MigrarTurmaResultadoDto } from '../../types/dtos';
import { PapelUsuario } from '../../types/enums';
import database from '../../database';
import Aluno from '../../database/models/Aluno';
import { Q } from '@nozbe/watermelondb';

function formatarDataInput(d: Date): string {
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

function toIsoUtc(d: Date): string {
    return new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())).toISOString();
}

export function MigracaoTurmaScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const { user } = useAuth();

    const [turmas, setTurmas] = useState<TurmaDto[]>([]);
    const [origem, setOrigem] = useState<TurmaDto | null>(null);
    const [destino, setDestino] = useState<TurmaDto | null>(null);
    const [alunos, setAlunos] = useState<Aluno[]>([]);
    const [alunosSelecionados, setAlunosSelecionados] = useState<Set<string>>(new Set());
    const [dataTransferencia, setDataTransferencia] = useState(formatarDataInput(new Date()));
    const [motivo, setMotivo] = useState('');

    const [carregandoTurmas, setCarregandoTurmas] = useState(false);
    const [carregandoAlunos, setCarregandoAlunos] = useState(false);
    const [executando, setExecutando] = useState(false);
    const [resultado, setResultado] = useState<MigrarTurmaResultadoDto | null>(null);

    const [etapa, setEtapa] = useState<'origem' | 'destino' | 'alunos' | 'confirmacao'>('origem');

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

    async function carregarAlunos(turmaId: string) {
        setCarregandoAlunos(true);
        try {
            const rows = await database
                .get<Aluno>('alunos')
                .query(Q.where('turma_id', turmaId))
                .fetch();
            const ordenados = rows.sort((a, b) => a.nome.localeCompare(b.nome));
            setAlunos(ordenados);
            setAlunosSelecionados(new Set(ordenados.map(a => a.id)));
        } catch {
            Alert.alert('Erro', 'Não foi possível carregar os alunos da turma de origem.');
        } finally {
            setCarregandoAlunos(false);
        }
    }

    useFocusEffect(
        useCallback(() => {
            if (user?.papel !== PapelUsuario.Administrador) {
                Alert.alert('Acesso negado', 'Apenas administradores podem migrar turmas.');
                navigation.goBack();
                return;
            }
            carregarTurmas();
        }, [user?.papel, navigation])
    );

    function selecionarOrigem(turma: TurmaDto) {
        setOrigem(turma);
        setDestino(null);
        setAlunos([]);
        setAlunosSelecionados(new Set());
        setResultado(null);
        setEtapa('destino');
    }

    function selecionarDestino(turma: TurmaDto) {
        if (origem && turma.id === origem.id) {
            Alert.alert('Atenção', 'A turma de destino deve ser diferente da turma de origem.');
            return;
        }
        setDestino(turma);
        setResultado(null);
        setEtapa('alunos');
        if (origem) {
            carregarAlunos(origem.id);
        }
    }

    function toggleSelecaoTodos() {
        if (alunosSelecionados.size === alunos.length) {
            setAlunosSelecionados(new Set());
        } else {
            setAlunosSelecionados(new Set(alunos.map(a => a.id)));
        }
    }

    function toggleSelecaoAluno(id: string) {
        const novo = new Set(alunosSelecionados);
        if (novo.has(id)) {
            novo.delete(id);
        } else {
            novo.add(id);
        }
        setAlunosSelecionados(novo);
    }

    function avancarParaConfirmacao() {
        if (alunosSelecionados.size === 0) {
            Alert.alert('Atenção', 'Selecione pelo menos um aluno para migrar.');
            return;
        }
        setEtapa('confirmacao');
    }

    async function executarMigracao() {
        if (!origem || !destino) return;

        const data = parseDataInput(dataTransferencia);
        if (!data) {
            Alert.alert('Atenção', 'Informe uma data de transferência válida no formato DD/MM/AAAA.');
            return;
        }

        const selecionados = alunos
            .filter(a => alunosSelecionados.has(a.id))
            .map(a => a.serverId || a.id)
            .filter(Boolean);

        if (selecionados.length === 0) {
            Alert.alert('Atenção', 'Nenhum aluno válido selecionado para migrar.');
            return;
        }

        Alert.alert(
            'Confirmar migração',
            `Deseja migrar ${selecionados.length} aluno(s) de ${origem.nome} para ${destino.nome}?`,
            [
                { text: 'Cancelar', style: 'cancel' },
                {
                    text: 'Migrar',
                    onPress: async () => {
                        setExecutando(true);
                        setResultado(null);
                        try {
                            const origemIdApi = origem.serverId || origem.id;
                            const destinoIdApi = destino.serverId || destino.id;
                            const resp = await api.post<MigrarTurmaResultadoDto>(
                                `/turmas/${origemIdApi}/migrar`,
                                {
                                    turmaOrigemId: origemIdApi,
                                    turmaDestinoId: destinoIdApi,
                                    dataTransferencia: toIsoUtc(data),
                                    motivo: motivo.trim() || undefined,
                                    alunosIds: selecionados,
                                }
                            );
                            setResultado(resp.data);
                            setEtapa('confirmacao');
                        } catch (err: any) {
                            const msg = err.response?.data?.detail
                                || err.response?.data?.message
                                || 'Não foi possível executar a migração. Verifique a conexão.';
                            Alert.alert('Erro', msg);
                        } finally {
                            setExecutando(false);
                        }
                    },
                },
            ]
        );
    }

    const turmasFiltradas = (excluirId?: string) =>
        turmas.filter(t => !excluirId || t.id !== excluirId);

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader title="Migração de Turma" onBack={() => navigation.goBack()} />

            <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
                {/* Passo 1: Turma origem */}
                <Surface style={styles.section} elevation={1}>
                    <View style={styles.sectionHeader}>
                        <View style={[styles.stepBadge, origem && styles.stepBadgeDone]}>
                            <Text variant="labelSmall" style={styles.stepBadgeText}>
                                {origem ? '✓' : '1'}
                            </Text>
                        </View>
                        <Text variant="labelLarge" style={styles.sectionTitle}>Turma de Origem</Text>
                    </View>

                    {origem ? (
                        <Surface style={styles.selectedBox} elevation={0}>
                            <View style={styles.selectedInfo}>
                                <MaterialCommunityIcons name="export" size={20} color={theme.colors.error} />
                                <Text variant="bodyMedium" style={styles.selectedText}>
                                    {origem.nome} — {origem.turno} — {origem.anoLetivo}
                                </Text>
                            </View>
                            <Button
                                mode="text"
                                compact
                                onPress={() => {
                                    setOrigem(null);
                                    setDestino(null);
                                    setAlunos([]);
                                    setAlunosSelecionados(new Set());
                                    setResultado(null);
                                    setEtapa('origem');
                                }}
                            >
                                Alterar
                            </Button>
                        </Surface>
                    ) : carregandoTurmas ? (
                        <View style={styles.loadingRow}>
                            <ActivityIndicator size="small" />
                            <Text variant="bodySmall" style={styles.loadingText}>Carregando turmas...</Text>
                        </View>
                    ) : (
                        turmasFiltradas().map(t => (
                            <Surface key={t.id} style={styles.listItem} elevation={0}>
                                <Button
                                    mode="text"
                                    onPress={() => selecionarOrigem(t)}
                                    contentStyle={styles.listItemContent}
                                    icon="chevron-right"
                                    style={styles.listItemButton}
                                >
                                    {`${t.nome} — ${t.turno} — ${t.anoLetivo}`}
                                </Button>
                            </Surface>
                        ))
                    )}
                </Surface>

                {/* Passo 2: Turma destino */}
                {origem && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={[styles.stepBadge, destino && styles.stepBadgeDone]}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>
                                    {destino ? '✓' : '2'}
                                </Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Turma de Destino</Text>
                        </View>

                        {destino ? (
                            <Surface style={styles.selectedBox} elevation={0}>
                                <View style={styles.selectedInfo}>
                                    <MaterialCommunityIcons name="import" size={20} color={theme.colors.success} />
                                    <Text variant="bodyMedium" style={styles.selectedText}>
                                        {destino.nome} — {destino.turno} — {destino.anoLetivo}
                                    </Text>
                                </View>
                                <Button
                                    mode="text"
                                    compact
                                    onPress={() => {
                                        setDestino(null);
                                        setAlunos([]);
                                        setAlunosSelecionados(new Set());
                                        setResultado(null);
                                        setEtapa('destino');
                                    }}
                                >
                                    Alterar
                                </Button>
                            </Surface>
                        ) : (
                            turmasFiltradas(origem.id).map(t => (
                                <Surface key={t.id} style={styles.listItem} elevation={0}>
                                    <Button
                                        mode="text"
                                        onPress={() => selecionarDestino(t)}
                                        contentStyle={styles.listItemContent}
                                        icon="chevron-right"
                                        style={styles.listItemButton}
                                    >
                                        {`${t.nome} — ${t.turno} — ${t.anoLetivo}`}
                                    </Button>
                                </Surface>
                            ))
                        )}
                    </Surface>
                )}

                {/* Passo 3: Seleção de alunos */}
                {origem && destino && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={[styles.stepBadge, etapa === 'alunos' && alunosSelecionados.size > 0 && styles.stepBadgeDone]}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>
                                    {alunosSelecionados.size > 0 ? '✓' : '3'}
                                </Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Alunos</Text>
                        </View>

                        {carregandoAlunos ? (
                            <View style={styles.loadingRow}>
                                <ActivityIndicator size="small" />
                                <Text variant="bodySmall" style={styles.loadingText}>Carregando alunos...</Text>
                            </View>
                        ) : alunos.length === 0 ? (
                            <EmptyState
                                icon="account-group-outline"
                                title="Nenhum aluno encontrado"
                                subtitle="A turma de origem não possui alunos ativos."
                            />
                        ) : (
                            <>
                                <Pressable
                                    style={styles.selecionarTodosRow}
                                    onPress={toggleSelecaoTodos}
                                >
                                    <Checkbox
                                        status={alunosSelecionados.size === alunos.length ? 'checked' : 'unchecked'}
                                    />
                                    <Text variant="bodyMedium" style={styles.selecionarTodosText}>
                                        {alunosSelecionados.size === alunos.length ? 'Desmarcar todos' : 'Selecionar todos'}
                                    </Text>
                                </Pressable>

                                {alunos.map(aluno => (
                                    <Pressable
                                        key={aluno.id}
                                        style={styles.alunoRow}
                                        onPress={() => toggleSelecaoAluno(aluno.id)}
                                    >
                                        <Checkbox
                                            status={alunosSelecionados.has(aluno.id) ? 'checked' : 'unchecked'}
                                        />
                                        <View style={styles.alunoInfo}>
                                            <Text variant="bodyMedium" style={styles.alunoNome}>{aluno.nome}</Text>
                                            {aluno.matricula ? (
                                                <Text variant="bodySmall" style={styles.alunoMatricula}>Matrícula: {aluno.matricula}</Text>
                                            ) : null}
                                        </View>
                                    </Pressable>
                                ))}

                                <Button
                                    mode="contained"
                                    onPress={avancarParaConfirmacao}
                                    icon="chevron-right"
                                    style={styles.actionButton}
                                    contentStyle={styles.actionButtonContent}
                                >
                                    Avançar
                                </Button>
                            </>
                        )}
                    </Surface>
                )}

                {/* Passo 4: Dados da transferência */}
                {origem && destino && etapa === 'confirmacao' && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <View style={styles.stepBadge}>
                                <Text variant="labelSmall" style={styles.stepBadgeText}>4</Text>
                            </View>
                            <Text variant="labelLarge" style={styles.sectionTitle}>Dados da Transferência</Text>
                        </View>

                        <TextInput
                            label="Data da transferência"
                            value={dataTransferencia}
                            onChangeText={setDataTransferencia}
                            placeholder="DD/MM/AAAA"
                            keyboardType="numeric"
                            maxLength={10}
                            mode="outlined"
                            style={styles.input}
                        />

                        <TextInput
                            label="Motivo (opcional)"
                            value={motivo}
                            onChangeText={setMotivo}
                            placeholder="Ex: Término do ano letivo — alunos aprovados"
                            mode="outlined"
                            style={styles.input}
                        />

                        <Button
                            mode="contained"
                            onPress={executarMigracao}
                            loading={executando}
                            disabled={executando}
                            icon="swap-horizontal"
                            style={styles.actionButton}
                            contentStyle={styles.actionButtonContent}
                        >
                            Executar Migração
                        </Button>
                    </Surface>
                )}

                {/* Resultado */}
                {resultado && (
                    <Surface style={styles.section} elevation={1}>
                        <View style={styles.sectionHeader}>
                            <MaterialCommunityIcons
                                name={resultado.quantidadeIgnorada > 0 ? 'alert-circle' : 'check-circle'}
                                size={20}
                                color={resultado.quantidadeIgnorada > 0 ? theme.colors.warning : theme.colors.success}
                            />
                            <Text variant="labelLarge" style={styles.sectionTitle}>Resultado</Text>
                        </View>

                        <View style={styles.resumo}>
                            <Surface style={[styles.resumoCard, { backgroundColor: theme.colors.successLight }]} elevation={0}>
                                <Text variant="headlineSmall" style={[styles.resumoNum, { color: theme.colors.success }]}>
                                    {resultado.quantidadeTransferida}
                                </Text>
                                <Text variant="labelSmall" style={[styles.resumoLabel, { color: theme.colors.success }]}>
                                    Transferidos
                                </Text>
                            </Surface>
                            <Surface style={[styles.resumoCard, { backgroundColor: theme.colors.warningLight }]} elevation={0}>
                                <Text variant="headlineSmall" style={[styles.resumoNum, { color: theme.colors.warning }]}>
                                    {resultado.quantidadeIgnorada}
                                </Text>
                                <Text variant="labelSmall" style={[styles.resumoLabel, { color: theme.colors.warning }]}>
                                    Ignorados
                                </Text>
                            </Surface>
                        </View>

                        {resultado.erros.length > 0 && (
                            <>
                                <Text variant="bodySmall" style={styles.errosTitulo}>Detalhes dos ignorados:</Text>
                                {resultado.erros.map((erro, idx) => (
                                    <Text key={idx} variant="bodySmall" style={styles.erroItem}>• {erro}</Text>
                                ))}
                            </>
                        )}

                        {resultado.quantidadeIgnorada === 0 && resultado.quantidadeTransferida > 0 && (
                            <EmptyState
                                icon="check-circle"
                                title="Migração concluída"
                                subtitle="Todos os alunos selecionados foram migrados com sucesso."
                            />
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
    selecionarTodosRow: {
        flexDirection: 'row',
        alignItems: 'center',
        paddingVertical: theme.spacing.sm,
        borderBottomWidth: 1,
        borderBottomColor: theme.colors.divider,
        marginBottom: theme.spacing.sm,
    },
    selecionarTodosText: {
        color: theme.colors.textPrimary,
        fontWeight: '600',
        marginLeft: theme.spacing.sm,
    },
    alunoRow: {
        flexDirection: 'row',
        alignItems: 'center',
        paddingVertical: theme.spacing.sm,
        borderBottomWidth: 1,
        borderBottomColor: theme.colors.divider,
    },
    alunoInfo: {
        marginLeft: theme.spacing.sm,
        flex: 1,
    },
    alunoNome: {
        color: theme.colors.textPrimary,
    },
    alunoMatricula: {
        color: theme.colors.textSecondary,
    },
    input: {
        backgroundColor: theme.colors.surface,
        marginBottom: theme.spacing.md,
    },
    actionButton: {
        borderRadius: theme.borderRadius.sm,
    },
    actionButtonContent: {
        paddingVertical: theme.spacing.xs,
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
    errosTitulo: {
        color: theme.colors.textSecondary,
        marginBottom: theme.spacing.sm,
    },
    erroItem: {
        color: theme.colors.textPrimary,
        marginBottom: 2,
    },
});
