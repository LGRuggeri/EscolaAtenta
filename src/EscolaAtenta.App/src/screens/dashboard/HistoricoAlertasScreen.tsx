import axios from 'axios';
import React, { useState, useCallback, useRef, useEffect } from 'react';
import {
    View,
    Text,
    StyleSheet,
    FlatList,
    ActivityIndicator,
    TouchableOpacity,
    Alert,
    TextInput,
} from 'react-native';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { AuditoriaAlertaDto } from '../../types/dtos';
import { TipoAlerta } from '../../types/enums';
import { alertasService } from '../../services/alertasService';
import { theme } from '../../theme/colors';

// ─────────────────────────────────────────────────────────────────────────────
// Constantes
// ─────────────────────────────────────────────────────────────────────────────
const PAGE_SIZE = 20;
const DEBOUNCE_MS = 500;

// ─────────────────────────────────────────────────────────────────────────────
// Tipos de filtro de tipo para a UI
// ─────────────────────────────────────────────────────────────────────────────
type TipoFiltro = 'Todos' | TipoAlerta.Evasao | TipoAlerta.Atraso;

const FILTROS_TIPO: { label: string; valor: TipoFiltro }[] = [
    { label: 'Todos', valor: 'Todos' },
    { label: '❌ Faltas', valor: TipoAlerta.Evasao },
    { label: '⏱️ Atrasos', valor: TipoAlerta.Atraso },
];

// ─────────────────────────────────────────────────────────────────────────────
// Helpers de apresentação
// ─────────────────────────────────────────────────────────────────────────────

function getNivelColor(nivelAlerta: string, tipoAlerta: string): string {
    if (tipoAlerta === 'Atraso') return nivelAlerta === 'Intermediario' ? theme.colors.textSecondary : theme.colors.textPrimary;
    switch (nivelAlerta) {
        case 'Preto': return theme.colors.textPrimary;
        case 'Vermelho': return theme.colors.error;
        case 'Intermediario': return theme.colors.primaryDark;
        case 'Aviso': return theme.colors.primary;
        default: return theme.colors.secondary;
    }
}

function getTituloExibicao(item: AuditoriaAlertaDto): string {
    if (item.tipoAlerta === 'Atraso') {
        return item.nivelAlerta === 'Intermediario' ? '⚠️ Atrasos Reincidentes' : '⏱️ Aviso de Atrasos';
    }
    switch (item.nivelAlerta) {
        case 'Preto': return '🛑 Risco Crítico - Ação Legal';
        case 'Vermelho': return '🚨 Alto Risco de Evasão';
        case 'Intermediario': return '⚠️ Alerta Intermediário';
        case 'Aviso': return '👀 Aviso de Faltas';
        default: return 'Alerta Escolar';
    }
}

function formatarData(isoDate: string): string {
    return new Date(isoDate).toLocaleDateString('pt-BR', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
    });
}

// ─────────────────────────────────────────────────────────────────────────────
// Sub-componentes
// ─────────────────────────────────────────────────────────────────────────────

function TipoBadge({ tipo }: { tipo: string }) {
    const isAtraso = tipo === 'Atraso';
    return (
        <View
            style={[styles.tipoBadge, isAtraso ? styles.tipoBadgeAtraso : styles.tipoBadgeFalta]}
            accessibilityLabel={isAtraso ? 'Tipo: Atraso' : 'Tipo: Falta'}
        >
            <Text style={[styles.tipoBadgeText, isAtraso ? styles.tipoBadgeTextAtraso : styles.tipoBadgeTextFalta]}>
                {isAtraso ? '⏱️ Atraso' : '❌ Falta'}
            </Text>
        </View>
    );
}

function AuditoriaCard({ item }: { item: AuditoriaAlertaDto }) {
    const borderColor = getNivelColor(item.nivelAlerta, item.tipoAlerta);
    const titulo = getTituloExibicao(item);
    const isAtraso = item.tipoAlerta === 'Atraso';

    return (
        <View
            style={[styles.card, { borderLeftColor: borderColor }, isAtraso && styles.cardAtraso]}
            accessibilityRole="text"
            accessibilityLabel={`Alerta resolvido de ${item.nomeAluno}. Resolvido por ${item.resolvidoPor}.`}
        >
            {/* Cabeçalho: título + badge de tipo */}
            <View style={styles.cardHeader}>
                <Text style={[styles.cardTitle, { color: borderColor }]} numberOfLines={1}>
                    {titulo}
                </Text>
                <TipoBadge tipo={item.tipoAlerta} />
            </View>

            {/* Meta: nome do aluno */}
            <View style={styles.cardMeta}>
                <Text style={styles.cardAluno} numberOfLines={1}>{item.nomeAluno}</Text>
                <Text style={styles.cardDataAlerta} numberOfLines={1}>
                    Alerta: {formatarData(item.dataAlerta)}
                </Text>
            </View>

            {/* Bloco de auditoria: responsável + data + motivo */}
            <View style={styles.auditBlock}>
                <View style={styles.auditHeaderRow}>
                    <Text style={styles.auditIdentificacao}>
                        ✅ Tratado por:{' '}
                        <Text style={styles.auditNomeUser}>{item.resolvidoPor}</Text>
                    </Text>
                    <Text style={styles.auditDate}>{formatarData(item.dataResolucao)}</Text>
                </View>

                {item.motivoResolucao ? (
                    <View style={styles.auditJustificativaBox}>
                        <Text style={styles.auditJustificativaIcon}>📝</Text>
                        <Text style={styles.auditJustificativaText} selectable>
                            "{item.motivoResolucao}"
                        </Text>
                    </View>
                ) : null}
            </View>
        </View>
    );
}

// ─────────────────────────────────────────────────────────────────────────────
// Tela principal
// ─────────────────────────────────────────────────────────────────────────────

export function HistoricoAlertasScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    // ── Estado dos dados ──────────────────────────────────────────────────────
    const [auditoriaList, setAuditoriaList] = useState<AuditoriaAlertaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);

    // ── Estado dos filtros ────────────────────────────────────────────────────
    const [searchQuery, setSearchQuery] = useState('');
    const [tipoFiltro, setTipoFiltro] = useState<TipoFiltro>('Todos');

    // ── Referências de controle (sem causar re-render) ────────────────────────
    const isFetchingRef = useRef(false);
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const abortControllerRef = useRef<AbortController | null>(null);
    // Ref espelho dos filtros para callbacks estáveis sem dependências circulares
    const searchQueryRef = useRef(searchQuery);
    const tipoFiltroRef = useRef(tipoFiltro);

    useEffect(() => { searchQueryRef.current = searchQuery; }, [searchQuery]);
    useEffect(() => { tipoFiltroRef.current = tipoFiltro; }, [tipoFiltro]);

    // ─────────────────────────────────────────────────────────────────────────
    // Fetch centralizado — sempre recebe os filtros como argumento
    // para evitar closure stale (captura de valores antigos em callbacks).
    // ─────────────────────────────────────────────────────────────────────────
    const fetchAuditoria = useCallback(async (
        page: number,
        nome: string,
        tipo: TipoFiltro,
        append: boolean  // true = infinite scroll, false = reset de lista
    ) => {
        // Controle de Race Condition: cancela requisição anterior se for uma nova busca
        if (!append) {
            if (abortControllerRef.current) {
                abortControllerRef.current.abort();
            }
            abortControllerRef.current = new AbortController();
        } else if (isFetchingRef.current) {
            return;
        }

        // Armazena a referência local para o finally block verificar
        const currentSignal = abortControllerRef.current?.signal;

        try {
            isFetchingRef.current = true;
            if (page === 1) setLoading(true);
            else setLoadingMore(true);

            const resultado = await alertasService.getAuditoriaAlertas({
                pageNumber: page,
                pageSize: PAGE_SIZE,
                ...(nome.trim() ? { nomeAluno: nome.trim() } : {}),
                ...(tipo !== 'Todos' ? { tipo: tipo as TipoAlerta } : {}),
                signal: currentSignal,
            });

            setAuditoriaList(prev =>
                append ? [...prev, ...resultado.items] : resultado.items
            );
            setCurrentPage(page);
            setHasNextPage(resultado.hasNextPage);
        } catch (err: any) {
            if (axios.isCancel(err)) {
                console.log('Requisição abortada (Race condition evitada)');
                return;
            }
            // 403 = Monitor sem permissão de acesso — mensagem específica
            if (err?.response?.status === 403) {
                Alert.alert('Acesso negado', 'Apenas Supervisão e Administrador podem ver a auditoria.');
            } else {
                Alert.alert('Erro', 'Não foi possível carregar a auditoria de alertas.');
            }
            console.error(err);
        } finally {
            // Só reseta loading states se ESTA chamada não foi abortada, 
            // evitando que o cancelamento desligue o state da nova requisição em andamento
            if (!currentSignal?.aborted) {
                setLoading(false);
                setLoadingMore(false);
                isFetchingRef.current = false;
            }
        }
    }, []);

    // ─────────────────────────────────────────────────────────────────────────
    // Carga inicial / recarregar ao retornar ao foco da tela
    // ─────────────────────────────────────────────────────────────────────────
    useFocusEffect(
        useCallback(() => {
            setAuditoriaList([]);
            setCurrentPage(1);
            fetchAuditoria(1, searchQueryRef.current, tipoFiltroRef.current, false);
        }, [fetchAuditoria])
    );

    // ─────────────────────────────────────────────────────────────────────────
    // Debounce no campo de busca por nome (500ms)
    // — dispara apenas quando o usuário PAROU de digitar,
    //   não a cada tecla pressionada.
    // ─────────────────────────────────────────────────────────────────────────
    const handleSearchChange = useCallback((texto: string) => {
        setSearchQuery(texto);

        if (debounceTimer.current) clearTimeout(debounceTimer.current);

        debounceTimer.current = setTimeout(() => {
            setAuditoriaList([]);
            setCurrentPage(1);
            fetchAuditoria(1, texto, tipoFiltroRef.current, false);
        }, DEBOUNCE_MS);
    }, [fetchAuditoria]);

    // ─────────────────────────────────────────────────────────────────────────
    // Troca de filtro de tipo — reset imediato de página e lista
    // ─────────────────────────────────────────────────────────────────────────
    const handleTipoChange = useCallback((novoTipo: TipoFiltro) => {
        if (novoTipo === tipoFiltroRef.current) return; // Sem mudança — não dispara
        setTipoFiltro(novoTipo);
        setAuditoriaList([]);
        setCurrentPage(1);
        fetchAuditoria(1, searchQueryRef.current, novoTipo, false);
    }, [fetchAuditoria]);

    // ─────────────────────────────────────────────────────────────────────────
    // Infinite Scroll — carrega próxima página ao chegar no final da lista
    // ─────────────────────────────────────────────────────────────────────────
    const handleEndReached = useCallback(() => {
        if (!hasNextPage || isFetchingRef.current || loading) return;
        const nextPage = currentPage + 1;
        fetchAuditoria(nextPage, searchQueryRef.current, tipoFiltroRef.current, true);
    }, [hasNextPage, currentPage, loading, fetchAuditoria]);

    // ─────────────────────────────────────────────────────────────────────────
    // Renders da FlatList
    // ─────────────────────────────────────────────────────────────────────────
    const renderItem = useCallback(
        ({ item }: { item: AuditoriaAlertaDto }) => <AuditoriaCard item={item} />,
        []
    );

    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" color={COLORS.primary} />
                <Text style={styles.footerLoaderText}>Carregando mais...</Text>
            </View>
        );
    };

    const renderListaVazia = () => {
        if (loading) return null;
        return (
            <View style={styles.emptyContainer}>
                <Text style={styles.emptyIcon}>🗄️</Text>
                <Text style={styles.emptyTitle}>Nenhum registro encontrado</Text>
                <Text style={styles.emptyText}>
                    {searchQuery.trim()
                        ? `Nenhum alerta resolvido para "${searchQuery}".`
                        : 'Nenhum alerta resolvido no histórico.'}
                </Text>
            </View>
        );
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────────────────────────
    return (
        <SafeAreaView style={styles.container}>
            {/* ── Cabeçalho ─────────────────────────────────────────────────── */}
            <View style={styles.header}>
                <TouchableOpacity
                    onPress={() => navigation.goBack()}
                    style={styles.backButton}
                    accessibilityRole="button"
                    accessibilityLabel="Voltar"
                >
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Auditoria de Alertas</Text>
                <View style={{ width: 60 }} />
            </View>

            {/* ── Barra de Filtros ────────────────────────────────────────── */}
            <View style={styles.filtersContainer}>
                {/* Busca por nome com debounce */}
                <View
                    style={styles.searchBox}
                    accessibilityLabel="Campo de busca por aluno"
                >
                    <Text style={styles.searchIcon}>🔍</Text>
                    <TextInput
                        style={styles.searchInput}
                        placeholder="Buscar por nome do aluno..."
                        placeholderTextColor={COLORS.textMuted}
                        value={searchQuery}
                        onChangeText={handleSearchChange}
                        returnKeyType="search"
                        autoCorrect={false}
                        autoCapitalize="words"
                        accessibilityLabel="Campo de busca por nome do aluno"
                    />
                    {searchQuery.length > 0 && (
                        <TouchableOpacity
                            onPress={() => handleSearchChange('')}
                            accessibilityRole="button"
                            accessibilityLabel="Limpar busca"
                        >
                            <Text style={styles.searchClear}>✕</Text>
                        </TouchableOpacity>
                    )}
                </View>

                {/* Filtros de tipo — toggles */}
                <View
                    style={styles.tipoFilterRow}
                    accessibilityRole="toolbar"
                    accessibilityLabel="Filtrar por tipo de alerta"
                >
                    {FILTROS_TIPO.map(({ label, valor }) => {
                        const ativo = tipoFiltro === valor;
                        return (
                            <TouchableOpacity
                                key={valor}
                                style={[styles.tipoFilterBtn, ativo && styles.tipoFilterBtnAtivo]}
                                onPress={() => handleTipoChange(valor)}
                                accessibilityRole="button"
                                accessibilityState={{ selected: ativo }}
                                accessibilityLabel={`Filtrar por ${label}`}
                                activeOpacity={0.7}
                            >
                                <Text style={[styles.tipoFilterText, ativo && styles.tipoFilterTextAtivo]}>
                                    {label}
                                </Text>
                            </TouchableOpacity>
                        );
                    })}
                </View>
            </View>

            {/* ── Lista de Auditoria ──────────────────────────────────────── */}
            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" color={COLORS.primary} />
                    <Text style={styles.loadingText}>Carregando auditoria...</Text>
                </View>
            ) : (
                <FlatList
                    data={auditoriaList}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={[
                        styles.listContainer,
                        auditoriaList.length === 0 && styles.listContainerEmpty,
                    ]}
                    ListEmptyComponent={renderListaVazia}
                    ListFooterComponent={renderListFooter}
                    onEndReached={handleEndReached}
                    onEndReachedThreshold={0.5}
                    removeClippedSubviews
                    initialNumToRender={10}
                    maxToRenderPerBatch={10}
                    windowSize={5}
                />
            )}
        </SafeAreaView>
    );
}

// ─────────────────────────────────────────────────────────────────────────────
// Design System — Tokens de cor (padrões institucionais do projeto)
// ─────────────────────────────────────────────────────────────────────────────
const COLORS = {
    bg: theme.colors.background,
    white: theme.colors.surface,
    primary: theme.colors.primary,         // Azul institucional
    primaryLight: theme.colors.surface,
    primaryBorder: theme.colors.border,
    indigo: theme.colors.primary,
    indigoLight: theme.colors.background,
    red: theme.colors.error,
    textPrimary: theme.colors.textPrimary,
    textSecondary: theme.colors.textSecondary,
    textMuted: theme.colors.textSecondary,
    border: theme.colors.border,
    inputBg: theme.colors.surface,
};

const styles = StyleSheet.create({
    // ── Layout base ──────────────────────────────────────────────────────────
    container: { flex: 1, backgroundColor: COLORS.bg },

    // ── Header ───────────────────────────────────────────────────────────────
    header: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
        paddingHorizontal: 20, paddingVertical: 16,
        backgroundColor: COLORS.white,
        elevation: 3, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 3,
    },
    backButton: {},
    backButtonText: { fontSize: 16, color: COLORS.textSecondary, fontWeight: '600' },
    headerTitle: { fontSize: 18, fontWeight: 'bold', color: COLORS.textPrimary },

    // ── Filtros ───────────────────────────────────────────────────────────────
    filtersContainer: {
        backgroundColor: COLORS.white,
        paddingHorizontal: 16,
        paddingTop: 12,
        paddingBottom: 8,
        borderBottomWidth: 1,
        borderBottomColor: COLORS.border,
        gap: 10,
    },
    searchBox: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: COLORS.bg,
        borderRadius: 10,
        borderWidth: 1,
        borderColor: COLORS.border,
        paddingHorizontal: 12,
        height: 44,
        gap: 8,
    },
    searchIcon: { fontSize: 16 },
    searchInput: {
        flex: 1,
        fontSize: 15,
        color: COLORS.textPrimary,
        paddingVertical: 0,
    },
    searchClear: { fontSize: 18, color: COLORS.textMuted, paddingHorizontal: 4 },

    tipoFilterRow: { flexDirection: 'row', gap: 8 },
    tipoFilterBtn: {
        flex: 1,
        paddingVertical: 8,
        borderRadius: 8,
        alignItems: 'center',
        backgroundColor: COLORS.bg,
        borderWidth: 1,
        borderColor: COLORS.border,
    },
    tipoFilterBtnAtivo: {
        backgroundColor: COLORS.primaryLight,
        borderColor: COLORS.primary,
    },
    tipoFilterText: { fontSize: 13, color: COLORS.textMuted, fontWeight: '600' },
    tipoFilterTextAtivo: { fontSize: 13, color: COLORS.primary, fontWeight: '700' },

    // ── Loading / Empty ───────────────────────────────────────────────────────
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { marginTop: 12, color: COLORS.textMuted, fontSize: 14 },
    footerLoader: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
        paddingVertical: 16, gap: 8,
    },
    footerLoaderText: { fontSize: 13, color: COLORS.textMuted },
    emptyContainer: { alignItems: 'center', paddingVertical: 48, paddingHorizontal: 32 },
    emptyIcon: { fontSize: 48, marginBottom: 12 },
    emptyTitle: { fontSize: 17, fontWeight: '700', color: COLORS.textSecondary, marginBottom: 6 },
    emptyText: { fontSize: 14, color: COLORS.textMuted, textAlign: 'center', lineHeight: 20 },

    // ── Lista ─────────────────────────────────────────────────────────────────
    listContainer: { paddingHorizontal: 16, paddingBottom: 40, paddingTop: 16 },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },

    // ── Card de auditoria ─────────────────────────────────────────────────────
    card: {
        backgroundColor: COLORS.white,
        padding: 16,
        borderRadius: 10,
        marginBottom: 12,
        borderLeftWidth: 5,
        elevation: 2,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 },
        shadowOpacity: 0.08,
        shadowRadius: 3,
    },
    cardAtraso: { backgroundColor: COLORS.indigoLight },
    cardHeader: {
        flexDirection: 'row', justifyContent: 'space-between',
        alignItems: 'center', marginBottom: 8, gap: 8,
    },
    cardTitle: { fontSize: 15, fontWeight: 'bold', flex: 1 },
    cardMeta: {
        flexDirection: 'row', gap: 8, marginBottom: 12,
        flexWrap: 'wrap', alignItems: 'center',
    },
    cardAluno: {
        fontSize: 13, fontWeight: '700', color: COLORS.textSecondary,
        backgroundColor: theme.colors.background, paddingHorizontal: 8, paddingVertical: 2, borderRadius: 6,
    },
    cardDataAlerta: {
        fontSize: 12, color: COLORS.textMuted,
        backgroundColor: theme.colors.background, paddingHorizontal: 8, paddingVertical: 2, borderRadius: 6,
    },

    // ── Badge de tipo ─────────────────────────────────────────────────────────
    tipoBadge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20 },
    tipoBadgeFalta: { backgroundColor: COLORS.red },
    tipoBadgeAtraso: { backgroundColor: COLORS.indigoLight },
    tipoBadgeText: { fontSize: 11, fontWeight: '700' },
    tipoBadgeTextFalta: { color: theme.colors.surface },
    tipoBadgeTextAtraso: { color: COLORS.indigo },

    // ── Bloco de auditoria dentro do card ─────────────────────────────────────
    auditBlock: {
        marginTop: 8,
        paddingTop: 12,
        borderTopWidth: 1,
        borderTopColor: theme.colors.border,
    },
    auditHeaderRow: {
        flexDirection: 'row', justifyContent: 'space-between',
        alignItems: 'center', marginBottom: 8,
    },
    auditIdentificacao: { fontSize: 13, color: COLORS.textSecondary, flex: 1, marginRight: 8 },
    auditNomeUser: { fontWeight: 'bold', color: COLORS.textPrimary },
    auditDate: { fontSize: 12, color: COLORS.textMuted },
    auditJustificativaBox: {
        flexDirection: 'row',
        backgroundColor: COLORS.bg,
        padding: 10,
        borderRadius: 8,
        borderLeftWidth: 3,
        borderLeftColor: theme.colors.border,
    },
    auditJustificativaIcon: { fontSize: 14, marginRight: 6, marginTop: 2 },
    auditJustificativaText: {
        flex: 1,
        fontSize: 13,
        color: theme.colors.textPrimary,
        fontStyle: 'italic',
        lineHeight: 18,
    },
});
