import React, { useState, useCallback, useMemo, useRef, useEffect } from 'react';
import {
    View,
    Text,
    StyleSheet,
    FlatList,
    ActivityIndicator,
    TouchableOpacity,
    Alert,
    Modal,
    TextInput,
    Pressable,
} from 'react-native';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { AlertaDto } from '../../types/dtos';
import { NivelAlertaFalta, PapelUsuario, TipoAlerta, parseNivelAlertaFalta } from '../../types/enums';
import { alertasService } from '../../services/alertasService';
import { useAuth } from '../../hooks/useAuth';

// ── Constantes de Paginação ───────────────────────────────────────────────────

const PAGE_SIZE = 20;

// ── Tipos Locais ───────────────────────────────────────────────────────────────

type FiltroAtivo = 'TODOS' | 'FALTAS' | 'ATRASOS';

interface FiltroTab {
    key: FiltroAtivo;
    label: string;
    icon: string;
}

const FILTROS: FiltroTab[] = [
    { key: 'TODOS', label: 'Todos', icon: '📋' },
    { key: 'FALTAS', label: 'Faltas', icon: '❌' },
    { key: 'ATRASOS', label: 'Atrasos', icon: '⏱️' },
];

// ── Helpers (fora do componente — não recriados a cada render) ─────────────────

/**
 * Retorna a cor de destaque baseada no tipo e nível do alerta.
 * Alertas de Atraso sempre recebem paleta índigo — sinaliza natureza diferente.
 */
function getBorderColor(item: AlertaDto): string {
    if (item.tipo === TipoAlerta.Atraso) {
        const nivel = parseNivelAlertaFalta(item.nivel);
        return nivel >= 2 ? '#6366F1' : '#818CF8';
    }
    const nivel = parseNivelAlertaFalta(item.nivel);
    if (nivel >= 5) return '#111827';
    switch (nivel) {
        case NivelAlertaFalta.Excelencia: return '#10B981';
        case NivelAlertaFalta.Aviso: return '#FBBF24';
        case NivelAlertaFalta.Intermediario: return '#F97316';
        case NivelAlertaFalta.Vermelho: return '#EF4444';
        default: return '#111827';
    }
}

/**
 * Retorna título amigável, priorizando o campo do backend.
 * Fallback local tipado por TipoAlerta enum — sem magic strings.
 */
function getTituloExibicao(item: AlertaDto): string {
    if (item.tituloAmigavel) return item.tituloAmigavel;
    if (item.tipo === TipoAlerta.Atraso) {
        const nivel = parseNivelAlertaFalta(item.nivel);
        return nivel >= 2 ? '⚠️ Atrasos Reincidentes' : '⏱️ Aviso de Atrasos';
    }
    const nivel = parseNivelAlertaFalta(item.nivel);
    if (nivel >= 5) return '🛑 Risco Crítico - Ação Legal';
    switch (nivel) {
        case NivelAlertaFalta.Aviso: return '👀 Aviso de Faltas';
        case NivelAlertaFalta.Intermediario: return '⚠️ Alerta Intermediário';
        case NivelAlertaFalta.Vermelho: return '🚨 Alto Risco de Evasão';
        default: return 'Alerta Escolar';
    }
}

/**
 * Placeholder do TextInput no modal — sensível ao tipo da infração.
 * Usa TipoAlerta enum: sem magic strings.
 */
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

// ── TipoBadge — Componente Presentacional ────────────────────────────────────

interface TipoBadgeProps { tipo: TipoAlerta }

function TipoBadge({ tipo }: TipoBadgeProps) {
    const isAtraso = tipo === TipoAlerta.Atraso;
    return (
        <View style={[styles.tipoBadge, isAtraso ? styles.tipoBadgeAtraso : styles.tipoBadgeFalta]}>
            <Text style={[styles.tipoBadgeText, isAtraso ? styles.tipoBadgeTextAtraso : styles.tipoBadgeTextFalta]}>
                {isAtraso ? '⏱️ Atraso' : '❌ Falta'}
            </Text>
        </View>
    );
}

// ── Componente Principal ───────────────────────────────────────────────────────

export function AlertasScreen() {
    const { user } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();

    // ── Estado: dados e paginação ─────────────────────────────────────────────
    const [alertas, setAlertas] = useState<AlertaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);  // Indicador do rodapé
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);
    const [filtroAtivo, setFiltroAtivo] = useState<FiltroAtivo>('TODOS');
    const [subFiltroNivel, setSubFiltroNivel] = useState<NivelAlertaFalta | null>(null);

    // Limpa o subfiltro se sair da aba Faltas
    useEffect(() => {
        if (filtroAtivo !== 'FALTAS') {
            setSubFiltroNivel(null);
        }
    }, [filtroAtivo]);

    // Ref para evitar chamadas duplicadas em onEndReached (React Native pode disparar twice)
    const isFetchingRef = useRef(false);

    // ── Estado: modal ─────────────────────────────────────────────────────────
    const [modalVisible, setModalVisible] = useState(false);
    const [alertaSelecionado, setAlertaSelecionado] = useState<AlertaDto | null>(null);
    const [tratativa, setTratativa] = useState('');
    const [resolvendo, setResolvendo] = useState(false);

    // ── Derivação de dados ────────────────────────────────────────────────────
    // useMemo garante que FlatList não receba novo array reference a cada render
    const alertasFiltrados = useMemo(() => {
        if (filtroAtivo === 'FALTAS') return alertas.filter(a => a.tipo === TipoAlerta.Evasao);
        if (filtroAtivo === 'ATRASOS') return alertas.filter(a => a.tipo === TipoAlerta.Atraso);
        return alertas;
    }, [alertas, filtroAtivo]);

    // Contadores para os badges nas abas — derivados em único useMemo
    const contadores = useMemo(() => ({
        todos: alertas.length,
        faltas: alertas.filter(a => a.tipo === TipoAlerta.Evasao).length,
        atrasos: alertas.filter(a => a.tipo === TipoAlerta.Atraso).length,
    }), [alertas]);

    // ── Carregamento inicial (página 1) ───────────────────────────────────────
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

    // ── Carregamento da próxima página (Infinite Scroll) ─────────────────────
    const carregarProximaPagina = useCallback(async () => {
        // Guards: evita chamadas duplicadas, chamadas sem mais páginas e durante load inicial
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

            // Concatena ao final — não substitui a lista (Infinite Scroll acumulativo)
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

    // Recarrega sempre que a tela ganha foco ou os filtros de API mudarem
    useFocusEffect(
        useCallback(() => {
            carregarAlertas();
        }, [carregarAlertas])
    );

    // ── Modal ─────────────────────────────────────────────────────────────────
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
            // Atualização otimista — remove da lista sem novo fetch
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

    // ── Renderização de Item ──────────────────────────────────────────────────
    const renderItem = ({ item }: { item: AlertaDto }) => {
        const borderColor = getBorderColor(item);
        const tituloExibicao = getTituloExibicao(item);
        const isAtraso = item.tipo === TipoAlerta.Atraso;

        return (
            <TouchableOpacity
                style={[
                    styles.card,
                    { borderLeftColor: borderColor },
                    isAtraso && styles.cardAtraso,
                ]}
                onPress={() => openModal(item)}
                accessibilityRole="button"
                accessibilityLabel={`Alerta de ${isAtraso ? 'atraso' : 'falta'} para ${item.nomeAluno}. Toque para resolver.`}
            >
                <View style={styles.cardHeader}>
                    <Text style={[styles.cardTitle, { color: borderColor }]} numberOfLines={1}>
                        {tituloExibicao}
                    </Text>
                    <Text style={styles.cardDate}>{formatData(item.dataAlerta)}</Text>
                </View>

                <View style={styles.cardMeta}>
                    <Text style={styles.cardAluno} numberOfLines={1}>{item.nomeAluno}</Text>
                    <Text style={styles.cardTurma} numberOfLines={1}>{item.nomeTurma}</Text>
                </View>

                <Text style={styles.cardDescricao} numberOfLines={2}>
                    {item.mensagemAcao || item.descricao}
                </Text>

                <View style={styles.cardFooter}>
                    <TipoBadge tipo={item.tipo} />
                    <View style={styles.resolverBadge}>
                        <Text style={styles.resolverBadgeText}>Toque para Resolver</Text>
                    </View>
                </View>
            </TouchableOpacity>
        );
    };

    // ── Rodapé da FlatList: spinner de "carregando mais" ─────────────────────
    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" color="#c9a227" />
                <Text style={styles.footerLoaderText}>Carregando mais alertas...</Text>
            </View>
        );
    };

    // ── Segmented Control ─────────────────────────────────────────────────────
    const renderSegmentedControl = () => (
        <View style={styles.segmentedContainer} accessibilityRole="tablist">
            {FILTROS.map((filtro) => {
                const isActive = filtroAtivo === filtro.key;
                const contador = filtro.key === 'TODOS'
                    ? contadores.todos
                    : filtro.key === 'FALTAS'
                        ? contadores.faltas
                        : contadores.atrasos;

                return (
                    <Pressable
                        key={filtro.key}
                        style={[styles.segmentedTab, isActive && styles.segmentedTabActive]}
                        onPress={() => setFiltroAtivo(filtro.key)}
                        accessibilityRole="tab"
                        accessibilityState={{ selected: isActive }}
                        accessibilityLabel={`${filtro.label}, ${contador} alertas`}
                    >
                        <Text style={[styles.segmentedTabText, isActive && styles.segmentedTabTextActive]}>
                            {filtro.icon} {filtro.label}
                        </Text>
                        {contador > 0 && (
                            <View style={[styles.tabBadge, isActive && styles.tabBadgeActive]}>
                                <Text style={[styles.tabBadgeText, isActive && styles.tabBadgeTextActive]}>
                                    {contador}
                                </Text>
                            </View>
                        )}
                    </Pressable>
                );
            })}
        </View>
    );

    // ── Subfiltro de Nível (Pílulas) ───────────────────────────────────────────
    const FILTROS_NIVEL = [
        { key: NivelAlertaFalta.Aviso, label: 'Amarelo', color: '#FBBF24', textColor: '#92400E' },
        { key: NivelAlertaFalta.Intermediario, label: 'Laranja', color: '#F97316', textColor: '#FFF' },
        { key: NivelAlertaFalta.Vermelho, label: 'Vermelho', color: '#EF4444', textColor: '#FFF' },
        { key: NivelAlertaFalta.Preto, label: 'Preto', color: '#111827', textColor: '#FFF' },
    ];

    const renderSubfiltroNivel = () => {
        if (filtroAtivo !== 'FALTAS') return null;

        return (
            <View style={styles.subfiltroContainer} accessibilityRole="tablist">
                {FILTROS_NIVEL.map((item) => {
                    const isActive = subFiltroNivel === item.key;
                    return (
                        <Pressable
                            key={item.key}
                            style={[
                                styles.subfiltroTab,
                                { backgroundColor: isActive ? item.color : COLORS.white },
                                isActive && styles.subfiltroTabActiveObj
                            ]}
                            onPress={() => setSubFiltroNivel(isActive ? null : item.key)}
                            accessibilityRole="button"
                            accessibilityState={{ selected: isActive }}
                        >
                            <Text style={[
                                styles.subfiltroTabText,
                                { color: isActive ? item.textColor : COLORS.textMuted },
                                isActive && styles.subfiltroTabTextActiveObj
                            ]}>
                                {item.label}
                            </Text>
                        </Pressable>
                    );
                })}
            </View>
        );
    };

    // ── Empty State contextual por filtro ─────────────────────────────────────
    const renderListaVazia = () => {
        const mensagens: Record<FiltroAtivo, { icon: string; texto: string }> = {
            TODOS: { icon: '✅', texto: 'Nenhuma situação de risco detectada.' },
            FALTAS: {
                icon: subFiltroNivel !== null ? '🔍' : '📗',
                texto: subFiltroNivel !== null
                    ? `Nenhum alerta ${FILTROS_NIVEL.find(f => f.key === subFiltroNivel)?.label.toLowerCase()} encontrado.`
                    : 'Nenhum alerta de falta pendente.'
            },
            ATRASOS: { icon: '🕐', texto: 'Nenhum alerta de atraso pendente.' },
        };
        const { icon, texto } = mensagens[filtroAtivo];
        return (
            <View style={styles.emptyContainer}>
                <Text style={styles.emptyIcon}>{icon}</Text>
                <Text style={styles.emptyText}>{texto}</Text>
            </View>
        );
    };

    // ── Render Principal ──────────────────────────────────────────────────────
    return (
        <SafeAreaView style={styles.container}>
            {/* Header */}
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Alertas Escolares</Text>
                <View style={{ width: 60 }} />
            </View>

            {/* Segmented Control */}
            {!loading && renderSegmentedControl()}
            {!loading && renderSubfiltroNivel()}

            {/* Lista com Infinite Scroll */}
            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" color="#c9a227" />
                    <Text style={styles.loadingText}>Buscando situações de risco...</Text>
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
                    ListEmptyComponent={renderListaVazia}
                    ListFooterComponent={renderListFooter}
                    // ── Infinite Scroll ──────────────────────────────────────
                    onEndReached={carregarProximaPagina}
                    onEndReachedThreshold={0.5}  // Dispara quando 50% do final é visível
                    // ── Otimizações de Performance ───────────────────────────
                    removeClippedSubviews
                    initialNumToRender={10}
                    maxToRenderPerBatch={10}
                    windowSize={5}
                />
            )}

            {/* Modal de Resolução */}
            <Modal
                animationType="slide"
                transparent={true}
                visible={modalVisible}
                onRequestClose={closeModal}
            >
                <View style={styles.modalOverlay}>
                    <View style={styles.modalContent}>
                        <View style={styles.modalHeader}>
                            <Text style={styles.modalTitle}>Resolver Alerta</Text>
                            {alertaSelecionado && <TipoBadge tipo={alertaSelecionado.tipo} />}
                        </View>

                        {alertaSelecionado && (
                            <View style={[
                                styles.modalAlertaInfo,
                                { borderLeftColor: getBorderColor(alertaSelecionado) }
                            ]}>
                                <Text style={styles.modalAlertaNome}>
                                    {alertaSelecionado.nomeAluno} · {alertaSelecionado.nomeTurma}
                                </Text>
                                <Text style={styles.modalAlertaDesc}>
                                    {alertaSelecionado.mensagemAcao || alertaSelecionado.descricao}
                                </Text>
                            </View>
                        )}

                        {user?.papel === PapelUsuario.Monitor ? (
                            <>
                                <View style={styles.monitorAvisoContainer}>
                                    <Text style={styles.monitorAvisoText}>
                                        📌 Apenas a supervisão pode registrar a tratativa e resolver este alerta.
                                    </Text>
                                </View>
                                <View style={styles.modalActions}>
                                    <TouchableOpacity style={[styles.modalButton, styles.modalButtonCancel]} onPress={closeModal}>
                                        <Text style={styles.modalButtonCancelText}>Fechar</Text>
                                    </TouchableOpacity>
                                </View>
                            </>
                        ) : (
                            <>
                                <Text style={styles.inputLabel}>Tratativa / Ação Tomada:</Text>
                                <TextInput
                                    style={styles.textInput}
                                    multiline
                                    numberOfLines={4}
                                    // Placeholder dinâmico — sensível ao contexto da infração
                                    // getPlaceholderTratativa usa TipoAlerta enum (sem magic strings)
                                    placeholder={getPlaceholderTratativa(alertaSelecionado?.tipo)}
                                    value={tratativa}
                                    onChangeText={setTratativa}
                                    accessibilityLabel="Campo de tratativa do alerta"
                                />

                                <View style={styles.modalActions}>
                                    <TouchableOpacity
                                        style={[styles.modalButton, styles.modalButtonCancel]}
                                        onPress={closeModal}
                                        disabled={resolvendo}
                                    >
                                        <Text style={styles.modalButtonCancelText}>Cancelar</Text>
                                    </TouchableOpacity>
                                    <TouchableOpacity
                                        style={[styles.modalButton, styles.modalButtonConfirm, resolvendo && { opacity: 0.7 }]}
                                        onPress={handleResolver}
                                        disabled={resolvendo}
                                        accessibilityState={{ disabled: resolvendo, busy: resolvendo }}
                                    >
                                        {resolvendo
                                            ? <ActivityIndicator color="#FFF" />
                                            : <Text style={styles.modalButtonConfirmText}>Confirmar</Text>
                                        }
                                    </TouchableOpacity>
                                </View>
                            </>
                        )}
                    </View>
                </View>
            </Modal>
        </SafeAreaView>
    );
}

// ── Estilos ────────────────────────────────────────────────────────────────────

const COLORS = {
    bg: '#F9FAFB',
    white: '#FFF',
    gold: '#c9a227',
    indigo: '#6366F1',
    indigoLight: '#EEF2FF',
    red: '#FEE2E2',
    textPrimary: '#111827',
    textSecondary: '#374151',
    textMuted: '#6B7280',
    border: '#E5E7EB',
    green: '#10B981',
};

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: COLORS.bg },

    // ── Header ──────────────────────────────────────────────────────────────
    header: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
        padding: 20, backgroundColor: COLORS.white,
        elevation: 2, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2,
    },
    backButton: {},
    backButtonText: { fontSize: 16, color: COLORS.textSecondary, fontWeight: '600' },
    headerTitle: { fontSize: 18, fontWeight: 'bold', color: COLORS.textPrimary },

    // ── Subfiltro (Pílulas) ──────────────────────────────────────────────────
    subfiltroContainer: {
        flexDirection: 'row', paddingHorizontal: 16, marginBottom: 12, gap: 8,
        justifyContent: 'center',
    },
    subfiltroTab: {
        paddingVertical: 6, paddingHorizontal: 12, borderRadius: 16,
        borderWidth: 1, borderColor: COLORS.border,
        elevation: 0,
    },
    subfiltroTabActiveObj: {
        borderColor: 'transparent',
    },
    subfiltroTabText: {
        fontSize: 12, fontWeight: '700',
    },
    subfiltroTabTextActiveObj: {
        fontWeight: 'bold',
    },

    // ── Segmented Control ────────────────────────────────────────────────────
    segmentedContainer: {
        flexDirection: 'row', margin: 16,
        backgroundColor: COLORS.white, borderRadius: 12, padding: 4,
        elevation: 1, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.06, shadowRadius: 2,
    },
    segmentedTab: {
        flex: 1, flexDirection: 'row', alignItems: 'center',
        justifyContent: 'center', paddingVertical: 10, paddingHorizontal: 8,
        borderRadius: 10, gap: 4,
    },
    segmentedTabActive: { backgroundColor: COLORS.gold },
    segmentedTabText: { fontSize: 13, fontWeight: '600', color: COLORS.textMuted },
    segmentedTabTextActive: { color: COLORS.white },
    tabBadge: {
        backgroundColor: COLORS.border, borderRadius: 10,
        minWidth: 20, height: 20, alignItems: 'center',
        justifyContent: 'center', paddingHorizontal: 5,
    },
    tabBadgeActive: { backgroundColor: 'rgba(255,255,255,0.3)' },
    tabBadgeText: { fontSize: 11, fontWeight: '700', color: COLORS.textSecondary },
    tabBadgeTextActive: { color: COLORS.white },

    // ── Loading ──────────────────────────────────────────────────────────────
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { marginTop: 12, color: COLORS.textMuted },

    // ── Rodapé: Infinite Scroll Indicator ────────────────────────────────────
    footerLoader: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
        paddingVertical: 16, gap: 8,
    },
    footerLoaderText: { fontSize: 13, color: COLORS.textMuted },

    // ── Lista ────────────────────────────────────────────────────────────────
    listContainer: { paddingHorizontal: 16, paddingBottom: 40 },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },

    // ── Card ─────────────────────────────────────────────────────────────────
    card: {
        backgroundColor: COLORS.white, padding: 16, borderRadius: 10,
        marginBottom: 12, borderLeftWidth: 5,
        elevation: 2, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.08, shadowRadius: 3,
    },
    cardAtraso: { backgroundColor: COLORS.indigoLight },
    cardHeader: {
        flexDirection: 'row', justifyContent: 'space-between',
        alignItems: 'flex-start', marginBottom: 8, gap: 8,
    },
    cardTitle: { fontSize: 15, fontWeight: 'bold', flex: 1 },
    cardDate: { fontSize: 11, color: COLORS.textMuted, flexShrink: 0 },
    cardMeta: { flexDirection: 'row', gap: 6, marginBottom: 6, flexWrap: 'wrap' },
    cardAluno: {
        fontSize: 13, fontWeight: '700', color: COLORS.textSecondary,
        backgroundColor: '#F3F4F6', paddingHorizontal: 8, paddingVertical: 2, borderRadius: 6,
    },
    cardTurma: {
        fontSize: 13, color: COLORS.textMuted,
        backgroundColor: '#F3F4F6', paddingHorizontal: 8, paddingVertical: 2, borderRadius: 6,
    },
    cardDescricao: { fontSize: 13, color: COLORS.textSecondary, lineHeight: 19, marginBottom: 12 },
    cardFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },

    // ── TipoBadge ────────────────────────────────────────────────────────────
    tipoBadge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20, alignSelf: 'flex-start' },
    tipoBadgeFalta: { backgroundColor: COLORS.red },
    tipoBadgeAtraso: { backgroundColor: COLORS.indigoLight },
    tipoBadgeText: { fontSize: 11, fontWeight: '700' },
    tipoBadgeTextFalta: { color: '#B91C1C' },
    tipoBadgeTextAtraso: { color: COLORS.indigo },

    // ── Resolver Badge ────────────────────────────────────────────────────────
    resolverBadge: { backgroundColor: '#F3F4F6', paddingHorizontal: 12, paddingVertical: 5, borderRadius: 16 },
    resolverBadgeText: { fontSize: 11, color: '#4B5563', fontWeight: '600' },

    // ── Empty State ───────────────────────────────────────────────────────────
    emptyContainer: { alignItems: 'center', paddingVertical: 48 },
    emptyIcon: { fontSize: 48, marginBottom: 16 },
    emptyText: { fontSize: 16, fontWeight: '600', color: COLORS.green, textAlign: 'center' },

    // ── Modal ─────────────────────────────────────────────────────────────────
    modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'center', padding: 20 },
    modalContent: {
        backgroundColor: COLORS.white, borderRadius: 16, padding: 24,
        elevation: 5, shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.25, shadowRadius: 4,
    },
    modalHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 },
    modalTitle: { fontSize: 20, fontWeight: 'bold', color: COLORS.textPrimary },
    modalAlertaInfo: { backgroundColor: '#F9FAFB', padding: 12, borderRadius: 8, marginBottom: 16, borderLeftWidth: 4 },
    modalAlertaNome: { fontSize: 13, fontWeight: '700', color: COLORS.textSecondary, marginBottom: 4 },
    modalAlertaDesc: { fontSize: 14, color: '#4B5563', lineHeight: 20 },
    inputLabel: { fontSize: 14, fontWeight: '600', color: COLORS.textSecondary, marginBottom: 8 },
    textInput: {
        borderWidth: 1, borderColor: COLORS.border, borderRadius: 8,
        padding: 12, fontSize: 14, color: COLORS.textPrimary,
        textAlignVertical: 'top', minHeight: 100, marginBottom: 20,
    },
    modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 12 },
    modalButton: { paddingVertical: 10, paddingHorizontal: 16, borderRadius: 8, minWidth: 100, alignItems: 'center' },
    modalButtonCancel: { backgroundColor: '#F3F4F6' },
    modalButtonCancelText: { color: '#4B5563', fontWeight: '600' },
    modalButtonConfirm: { backgroundColor: COLORS.green },
    modalButtonConfirmText: { color: COLORS.white, fontWeight: 'bold' },
    monitorAvisoContainer: { backgroundColor: '#FEF3C7', padding: 12, borderRadius: 8, marginBottom: 20 },
    monitorAvisoText: { color: '#92400E', fontSize: 14, lineHeight: 20 },
});
