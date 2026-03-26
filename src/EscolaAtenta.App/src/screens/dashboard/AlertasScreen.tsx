import React, { useState, useCallback, useMemo, useRef, useEffect } from 'react';
import {
    View,
    StyleSheet,
    FlatList,
    Alert,
    Pressable,
} from 'react-native';
import {
    Text,
    Button,
    ActivityIndicator,
    Surface,
    Modal,
    Portal,
    TextInput,
    Chip,
    SegmentedButtons,
    Badge,
} from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { AlertaDto } from '../../types/dtos';
import { NivelAlertaFalta, PapelUsuario, TipoAlerta, parseNivelAlertaFalta } from '../../types/enums';
import { alertasService } from '../../services/alertasService';
import { useAuth } from '../../hooks/useAuth';
import { AppHeader, StatusChip } from '../../components/ui';
import { theme, palette } from '../../theme/colors';

const PAGE_SIZE = 20;

type FiltroAtivo = 'TODOS' | 'FALTAS' | 'ATRASOS';

function getBorderColor(item: AlertaDto): string {
    if (item.tipo === TipoAlerta.Atraso) {
        const nivel = parseNivelAlertaFalta(item.nivel);
        return nivel >= 2 ? theme.colors.warning : theme.colors.border;
    }
    const nivel = parseNivelAlertaFalta(item.nivel);
    if (nivel >= 5) return theme.colors.primaryDark;
    switch (nivel) {
        case NivelAlertaFalta.Excelencia: return theme.colors.secondary;
        case NivelAlertaFalta.Aviso: return theme.colors.secondaryLight;
        case NivelAlertaFalta.Intermediario: return theme.colors.primary;
        case NivelAlertaFalta.Vermelho: return theme.colors.error;
        default: return theme.colors.primary;
    }
}

function getTituloExibicao(item: AlertaDto): string {
    if (item.tituloAmigavel) return item.tituloAmigavel;
    if (item.tipo === TipoAlerta.Atraso) {
        const nivel = parseNivelAlertaFalta(item.nivel);
        return nivel >= 2 ? 'Atrasos Reincidentes' : 'Aviso de Atrasos';
    }
    const nivel = parseNivelAlertaFalta(item.nivel);
    if (nivel >= 5) return 'Risco Crítico - Ação Legal';
    switch (nivel) {
        case NivelAlertaFalta.Aviso: return 'Aviso de Faltas';
        case NivelAlertaFalta.Intermediario: return 'Alerta Intermediário';
        case NivelAlertaFalta.Vermelho: return 'Alto Risco de Evasão';
        default: return 'Alerta Escolar';
    }
}

function getPlaceholderTratativa(tipo: TipoAlerta | undefined): string {
    if (tipo === TipoAlerta.Atraso) {
        return 'Descreva a orientação dada ao aluno sobre pontualidade e regras da escola...';
    }
    return 'Descreva a ligação feita aos pais, a conversa com o aluno, providências tomadas, etc...';
}

function formatData(isoDate: string): string {
    return new Date(isoDate).toLocaleDateString('pt-BR', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
    });
}

function getAlertIcon(item: AlertaDto): keyof typeof MaterialCommunityIcons.glyphMap {
    if (item.tipo === TipoAlerta.Atraso) return 'clock-alert-outline';
    const nivel = parseNivelAlertaFalta(item.nivel);
    if (nivel >= 5) return 'alert-octagon';
    if (nivel >= NivelAlertaFalta.Vermelho) return 'alert-circle';
    if (nivel >= NivelAlertaFalta.Intermediario) return 'alert';
    return 'information-outline';
}

export function AlertasScreen() {
    const { user } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();

    const [alertas, setAlertas] = useState<AlertaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);
    const [filtroAtivo, setFiltroAtivo] = useState<FiltroAtivo>('TODOS');
    const [subFiltroNivel, setSubFiltroNivel] = useState<NivelAlertaFalta | null>(null);

    useEffect(() => {
        if (filtroAtivo !== 'FALTAS') setSubFiltroNivel(null);
    }, [filtroAtivo]);

    const isFetchingRef = useRef(false);

    const [modalVisible, setModalVisible] = useState(false);
    const [alertaSelecionado, setAlertaSelecionado] = useState<AlertaDto | null>(null);
    const [tratativa, setTratativa] = useState('');
    const [resolvendo, setResolvendo] = useState(false);

    const alertasFiltrados = useMemo(() => {
        if (filtroAtivo === 'FALTAS') return alertas.filter(a => a.tipo === TipoAlerta.Evasao);
        if (filtroAtivo === 'ATRASOS') return alertas.filter(a => a.tipo === TipoAlerta.Atraso);
        return alertas;
    }, [alertas, filtroAtivo]);

    const contadores = useMemo(() => ({
        todos: alertas.length,
        faltas: alertas.filter(a => a.tipo === TipoAlerta.Evasao).length,
        atrasos: alertas.filter(a => a.tipo === TipoAlerta.Atraso).length,
    }), [alertas]);

    const carregarAlertas = useCallback(async () => {
        try {
            setLoading(true);
            isFetchingRef.current = true;

            const params: import('../../services/alertasService').GetAlertasParams = {
                pageNumber: 1,
                pageSize: PAGE_SIZE
            };

            if (subFiltroNivel !== null && filtroAtivo === 'FALTAS') {
                params.tipo = TipoAlerta.Evasao;
                params.nivel = subFiltroNivel;
            }

            const resultado = await alertasService.obterPaginados(params);
            setAlertas(resultado.items);
            setCurrentPage(1);
            setHasNextPage(resultado.hasNextPage);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar os alertas.');
            console.error(error);
        } finally {
            setLoading(false);
            isFetchingRef.current = false;
        }
    }, [subFiltroNivel, filtroAtivo]);

    const carregarProximaPagina = useCallback(async () => {
        if (isFetchingRef.current || !hasNextPage || loading) return;

        try {
            isFetchingRef.current = true;
            setLoadingMore(true);
            const nextPage = currentPage + 1;

            const params: import('../../services/alertasService').GetAlertasParams = {
                pageNumber: nextPage,
                pageSize: PAGE_SIZE
            };

            if (subFiltroNivel !== null && filtroAtivo === 'FALTAS') {
                params.tipo = TipoAlerta.Evasao;
                params.nivel = subFiltroNivel;
            }

            const resultado = await alertasService.obterPaginados(params);
            setAlertas(prev => [...prev, ...resultado.items]);
            setCurrentPage(nextPage);
            setHasNextPage(resultado.hasNextPage);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar mais alertas.');
            console.error(error);
        } finally {
            setLoadingMore(false);
            isFetchingRef.current = false;
        }
    }, [hasNextPage, currentPage, loading, subFiltroNivel, filtroAtivo]);

    useFocusEffect(
        useCallback(() => {
            carregarAlertas();
        }, [carregarAlertas])
    );

    const openModal = (alerta: AlertaDto) => {
        setAlertaSelecionado(alerta);
        setTratativa('');
        setModalVisible(true);
    };

    const closeModal = () => {
        setModalVisible(false);
        setAlertaSelecionado(null);
        setTratativa('');
    };

    const handleResolver = async () => {
        if (!alertaSelecionado) return;
        if (!tratativa.trim()) {
            Alert.alert('Aviso', 'Por favor, informe a tratativa ou ação tomada.');
            return;
        }
        try {
            setResolvendo(true);
            await alertasService.resolver(alertaSelecionado.id, tratativa);
            setAlertas(prev => prev.filter(a => a.id !== alertaSelecionado.id));
            Alert.alert('Sucesso', 'Alerta resolvido com sucesso!');
            closeModal();
        } catch (error) {
            Alert.alert('Erro', 'Ocorreu um problema ao resolver o alerta.');
            console.error(error);
        } finally {
            setResolvendo(false);
        }
    };

    // ── Subfiltro de Nível (Chips) ───────────────────────────────
    const FILTROS_NIVEL: { key: NivelAlertaFalta; label: string; variant: 'success' | 'warning' | 'error' | 'info' }[] = [
        { key: NivelAlertaFalta.Aviso, label: 'Alerta', variant: 'warning' },
        { key: NivelAlertaFalta.Intermediario, label: 'Intermediário', variant: 'info' },
        { key: NivelAlertaFalta.Vermelho, label: 'Crítico', variant: 'error' },
        { key: NivelAlertaFalta.Preto, label: 'Ação Legal', variant: 'error' },
    ];

    const renderItem = ({ item }: { item: AlertaDto }) => {
        const borderColor = getBorderColor(item);
        const tituloExibicao = getTituloExibicao(item);
        const isAtraso = item.tipo === TipoAlerta.Atraso;
        const alertIcon = getAlertIcon(item);

        return (
            <Pressable onPress={() => openModal(item)}>
                <Surface
                    style={[styles.alertCard, { borderLeftColor: borderColor }]}
                    elevation={1}
                >
                    <View style={styles.alertHeader}>
                        <View style={styles.alertTitleRow}>
                            <MaterialCommunityIcons name={alertIcon} size={18} color={borderColor} />
                            <Text variant="titleSmall" style={[styles.alertTitle, { color: borderColor }]} numberOfLines={1}>
                                {tituloExibicao}
                            </Text>
                        </View>
                        <Text variant="labelSmall" style={styles.alertDate}>{formatData(item.dataAlerta)}</Text>
                    </View>

                    <View style={styles.alertMeta}>
                        <Chip compact icon="account" textStyle={styles.metaChipText} style={styles.metaChip}>
                            {item.nomeAluno}
                        </Chip>
                        <Chip compact icon="google-classroom" textStyle={styles.metaChipText} style={styles.metaChip}>
                            {item.nomeTurma}
                        </Chip>
                    </View>

                    <Text variant="bodySmall" style={styles.alertDesc} numberOfLines={2}>
                        {item.mensagemAcao || item.descricao}
                    </Text>

                    <View style={styles.alertFooter}>
                        <StatusChip
                            label={isAtraso ? 'Atraso' : 'Falta'}
                            variant={isAtraso ? 'warning' : 'error'}
                            icon={isAtraso ? 'clock-alert-outline' : 'close-circle-outline'}
                        />
                        <Text variant="labelSmall" style={styles.tapHint}>
                            {user?.papel === PapelUsuario.Monitor ? 'Visualizar' : 'Resolver'}
                        </Text>
                    </View>
                </Surface>
            </Pressable>
        );
    };

    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" />
                <Text variant="labelSmall" style={styles.footerText}>Carregando mais...</Text>
            </View>
        );
    };

    const renderEmptyState = () => {
        const configs: Record<FiltroAtivo, { icon: keyof typeof MaterialCommunityIcons.glyphMap; text: string }> = {
            TODOS: { icon: 'check-circle-outline', text: 'Nenhuma situação de risco detectada.' },
            FALTAS: { icon: 'book-check-outline', text: 'Nenhum alerta de falta pendente.' },
            ATRASOS: { icon: 'clock-check-outline', text: 'Nenhum alerta de atraso pendente.' },
        };
        const { icon, text } = configs[filtroAtivo];
        return (
            <View style={styles.emptyContainer}>
                <MaterialCommunityIcons name={icon} size={56} color={theme.colors.success} style={{ opacity: 0.7 }} />
                <Text variant="titleMedium" style={styles.emptyText}>{text}</Text>
            </View>
        );
    };

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title="Alertas Escolares"
                onBack={() => navigation.goBack()}
                rightActions={[{
                    icon: 'history',
                    onPress: () => navigation.navigate('HistoricoAlertas'),
                    label: 'Auditoria'
                }]}
            />

            {/* Segmented Tabs */}
            {!loading && (
                <View style={styles.segmentedWrapper}>
                    <SegmentedButtons
                        value={filtroAtivo}
                        onValueChange={(v) => setFiltroAtivo(v as FiltroAtivo)}
                        buttons={[
                            { value: 'TODOS', label: `Todos (${contadores.todos})`, icon: 'format-list-bulleted' },
                            { value: 'FALTAS', label: `Faltas (${contadores.faltas})`, icon: 'close-circle-outline' },
                            { value: 'ATRASOS', label: `Atrasos (${contadores.atrasos})`, icon: 'clock-alert-outline' },
                        ]}
                        style={styles.segmented}
                    />
                </View>
            )}

            {/* Subfiltro Chips */}
            {!loading && filtroAtivo === 'FALTAS' && (
                <View style={styles.subfiltroRow}>
                    {FILTROS_NIVEL.map((item) => {
                        const isActive = subFiltroNivel === item.key;
                        return (
                            <Chip
                                key={item.key}
                                selected={isActive}
                                showSelectedOverlay
                                onPress={() => setSubFiltroNivel(isActive ? null : item.key)}
                                compact
                                style={styles.subfiltroChip}
                            >
                                {item.label}
                            </Chip>
                        );
                    })}
                </View>
            )}

            {/* Lista */}
            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" />
                    <Text variant="bodyMedium" style={styles.loadingText}>Buscando situações de risco...</Text>
                </View>
            ) : (
                <FlatList
                    data={alertasFiltrados}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={[
                        styles.listContainer,
                        alertasFiltrados.length === 0 && styles.listContainerEmpty,
                    ]}
                    ListEmptyComponent={renderEmptyState}
                    ListFooterComponent={renderListFooter}
                    onEndReached={carregarProximaPagina}
                    onEndReachedThreshold={0.5}
                    removeClippedSubviews
                    initialNumToRender={10}
                    maxToRenderPerBatch={10}
                    windowSize={5}
                />
            )}

            {/* Modal de Resolução */}
            <Portal>
                <Modal
                    visible={modalVisible}
                    onDismiss={closeModal}
                    contentContainerStyle={styles.modalContent}
                >
                    <View style={styles.modalHeader}>
                        <Text variant="titleLarge" style={styles.modalTitle}>Resolver Alerta</Text>
                        {alertaSelecionado && (
                            <StatusChip
                                label={alertaSelecionado.tipo === TipoAlerta.Atraso ? 'Atraso' : 'Falta'}
                                variant={alertaSelecionado.tipo === TipoAlerta.Atraso ? 'warning' : 'error'}
                            />
                        )}
                    </View>

                    {alertaSelecionado && (
                        <Surface style={[styles.modalAlertInfo, { borderLeftColor: getBorderColor(alertaSelecionado) }]} elevation={0}>
                            <Text variant="labelMedium" style={styles.modalAlertNome}>
                                {alertaSelecionado.nomeAluno} · {alertaSelecionado.nomeTurma}
                            </Text>
                            <Text variant="bodySmall" style={styles.modalAlertDesc}>
                                {alertaSelecionado.mensagemAcao || alertaSelecionado.descricao}
                            </Text>
                        </Surface>
                    )}

                    {user?.papel === PapelUsuario.Monitor ? (
                        <>
                            <Surface style={styles.monitorAviso} elevation={0}>
                                <MaterialCommunityIcons name="information" size={20} color={theme.colors.primary} />
                                <Text variant="bodySmall" style={styles.monitorAvisoText}>
                                    Apenas a supervisão pode registrar a tratativa e resolver este alerta.
                                </Text>
                            </Surface>
                            <Button mode="outlined" onPress={closeModal}>Fechar</Button>
                        </>
                    ) : (
                        <>
                            <TextInput
                                label="Tratativa / Ação Tomada"
                                value={tratativa}
                                onChangeText={setTratativa}
                                multiline
                                numberOfLines={4}
                                placeholder={getPlaceholderTratativa(alertaSelecionado?.tipo)}
                                maxLength={500}
                                mode="outlined"
                                style={styles.treatmentInput}
                            />

                            <View style={styles.modalActions}>
                                <Button
                                    mode="text"
                                    onPress={closeModal}
                                    disabled={resolvendo}
                                >
                                    Cancelar
                                </Button>
                                <Button
                                    mode="contained"
                                    onPress={handleResolver}
                                    loading={resolvendo}
                                    disabled={resolvendo}
                                    icon="check"
                                    buttonColor={theme.colors.secondary}
                                >
                                    Confirmar
                                </Button>
                            </View>
                        </>
                    )}
                </Modal>
            </Portal>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },

    segmentedWrapper: { paddingHorizontal: theme.spacing.md, paddingVertical: theme.spacing.sm },
    segmented: {},

    subfiltroRow: {
        flexDirection: 'row',
        paddingHorizontal: theme.spacing.md,
        marginBottom: theme.spacing.sm,
        gap: theme.spacing.sm,
        justifyContent: 'center',
    },
    subfiltroChip: { backgroundColor: theme.colors.surface },

    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { color: theme.colors.textSecondary, marginTop: theme.spacing.md },

    footerLoader: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        paddingVertical: theme.spacing.md,
        gap: theme.spacing.sm,
    },
    footerText: { color: theme.colors.textSecondary },

    listContainer: { paddingHorizontal: theme.spacing.md, paddingBottom: theme.spacing.xl },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },

    alertCard: {
        backgroundColor: theme.colors.surface,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.md,
        marginBottom: theme.spacing.sm + 4,
        borderLeftWidth: 4,
    },
    alertHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: theme.spacing.sm,
    },
    alertTitleRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        flex: 1,
    },
    alertTitle: { fontWeight: 'bold', flex: 1 },
    alertDate: { color: theme.colors.textMuted, flexShrink: 0 },
    alertMeta: { flexDirection: 'row', gap: theme.spacing.sm, marginBottom: theme.spacing.sm, flexWrap: 'wrap' },
    metaChip: { backgroundColor: theme.colors.surfaceVariant },
    metaChipText: { fontSize: 12 },
    alertDesc: { color: theme.colors.textSecondary, lineHeight: 19, marginBottom: theme.spacing.sm + 4 },
    alertFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    tapHint: { color: theme.colors.textMuted },

    emptyContainer: { alignItems: 'center', paddingVertical: theme.spacing.xxl },
    emptyText: { color: theme.colors.secondary, fontWeight: '600', marginTop: theme.spacing.md, textAlign: 'center' },

    modalContent: {
        backgroundColor: theme.colors.surface,
        margin: theme.spacing.lg,
        padding: theme.spacing.lg,
        borderRadius: theme.borderRadius.xl,
    },
    modalHeader: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: theme.spacing.md,
    },
    modalTitle: { fontWeight: 'bold', color: theme.colors.textPrimary },
    modalAlertInfo: {
        backgroundColor: theme.colors.surfaceVariant,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.sm,
        marginBottom: theme.spacing.md,
        borderLeftWidth: 4,
    },
    modalAlertNome: { fontWeight: '700', color: theme.colors.textSecondary, marginBottom: 4 },
    modalAlertDesc: { color: theme.colors.textMuted, lineHeight: 20 },
    treatmentInput: { marginBottom: theme.spacing.md, backgroundColor: theme.colors.surface },
    modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: theme.spacing.sm },
    monitorAviso: {
        flexDirection: 'row',
        backgroundColor: theme.colors.infoLight,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.sm,
        marginBottom: theme.spacing.md,
        gap: theme.spacing.sm,
        alignItems: 'flex-start',
    },
    monitorAvisoText: { color: theme.colors.primary, flex: 1, lineHeight: 20 },
});
