import axios from 'axios';
import React, { useState, useCallback, useRef, useEffect } from 'react';
import { View, StyleSheet, FlatList, Alert } from 'react-native';
import { Text, Searchbar, ActivityIndicator, Surface, Chip } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { AuditoriaAlertaDto } from '../../types/dtos';
import { TipoAlerta } from '../../types/enums';
import { alertasService } from '../../services/alertasService';
import { AppHeader, EmptyState, StatusChip } from '../../components/ui';
import { theme } from '../../theme/colors';

const PAGE_SIZE = 20;
const DEBOUNCE_MS = 500;

type TipoFiltro = 'Todos' | TipoAlerta.Evasao | TipoAlerta.Atraso;

const FILTROS_TIPO: { label: string; valor: TipoFiltro; icon: string }[] = [
    { label: 'Todos', valor: 'Todos', icon: 'filter-variant' },
    { label: 'Faltas', valor: TipoAlerta.Evasao, icon: 'close-circle-outline' },
    { label: 'Atrasos', valor: TipoAlerta.Atraso, icon: 'clock-alert-outline' },
];

function getNivelIcon(nivelAlerta: string, tipoAlerta: string): { name: string; color: string } {
    if (tipoAlerta === 'Atraso') {
        return nivelAlerta === 'Intermediario'
            ? { name: 'alert', color: theme.colors.warning }
            : { name: 'clock-alert', color: theme.colors.info };
    }
    switch (nivelAlerta) {
        case 'Preto': return { name: 'alert-octagon', color: theme.colors.textPrimary };
        case 'Vermelho': return { name: 'alert-circle', color: theme.colors.error };
        case 'Intermediario': return { name: 'alert', color: theme.colors.warning };
        case 'Aviso': return { name: 'eye-outline', color: theme.colors.primary };
        default: return { name: 'bell-outline', color: theme.colors.secondary };
    }
}

function getTituloExibicao(item: AuditoriaAlertaDto): string {
    if (item.tipoAlerta === 'Atraso') {
        return item.nivelAlerta === 'Intermediario' ? 'Atrasos Reincidentes' : 'Aviso de Atrasos';
    }
    switch (item.nivelAlerta) {
        case 'Preto': return 'Risco Crítico - Ação Legal';
        case 'Vermelho': return 'Alto Risco de Evasão';
        case 'Intermediario': return 'Alerta Intermediário';
        case 'Aviso': return 'Aviso de Faltas';
        default: return 'Alerta Escolar';
    }
}

function formatarData(isoDate: string): string {
    return new Date(isoDate).toLocaleDateString('pt-BR', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
    });
}

function AuditoriaCard({ item }: { item: AuditoriaAlertaDto }) {
    const nivelIcon = getNivelIcon(item.nivelAlerta, item.tipoAlerta);
    const titulo = getTituloExibicao(item);
    const isAtraso = item.tipoAlerta === 'Atraso';

    return (
        <Surface
            style={[styles.card, { borderLeftColor: nivelIcon.color }]}
            elevation={2}
        >
            {/* Header: título + badge de tipo */}
            <View style={styles.cardHeader}>
                <View style={styles.cardTitleRow}>
                    <MaterialCommunityIcons name={nivelIcon.name as any} size={18} color={nivelIcon.color} />
                    <Text variant="titleSmall" style={[styles.cardTitle, { color: nivelIcon.color }]} numberOfLines={1}>
                        {titulo}
                    </Text>
                </View>
                <StatusChip
                    label={isAtraso ? 'Atraso' : 'Falta'}
                    variant={isAtraso ? 'warning' : 'error'}
                />
            </View>

            {/* Meta: nome do aluno */}
            <View style={styles.cardMeta}>
                <Chip
                    compact
                    icon="account"
                    textStyle={styles.metaChipText}
                    style={styles.metaChip}
                >
                    {item.nomeAluno}
                </Chip>
                <Chip
                    compact
                    icon="calendar"
                    textStyle={styles.metaChipText}
                    style={styles.metaChip}
                >
                    {formatarData(item.dataAlerta)}
                </Chip>
            </View>

            {/* Bloco de auditoria */}
            <View style={styles.auditBlock}>
                <View style={styles.auditHeaderRow}>
                    <View style={styles.auditIdentRow}>
                        <MaterialCommunityIcons name="check-decagram" size={16} color={theme.colors.success} />
                        <Text variant="bodySmall" style={styles.auditIdentText}>
                            Tratado por: <Text style={styles.auditNomeUser}>{item.resolvidoPor}</Text>
                        </Text>
                    </View>
                    <Text variant="labelSmall" style={styles.auditDate}>
                        {formatarData(item.dataResolucao)}
                    </Text>
                </View>

                {item.motivoResolucao ? (
                    <Surface style={styles.auditJustificativaBox} elevation={0}>
                        <MaterialCommunityIcons name="text-box-outline" size={16} color={theme.colors.textSecondary} />
                        <Text variant="bodySmall" style={styles.auditJustificativaText} selectable>
                            "{item.motivoResolucao}"
                        </Text>
                    </Surface>
                ) : null}
            </View>
        </Surface>
    );
}

export function HistoricoAlertasScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    const [auditoriaList, setAuditoriaList] = useState<AuditoriaAlertaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);

    const [searchQuery, setSearchQuery] = useState('');
    const [tipoFiltro, setTipoFiltro] = useState<TipoFiltro>('Todos');

    const isFetchingRef = useRef(false);
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const abortControllerRef = useRef<AbortController | null>(null);
    const searchQueryRef = useRef(searchQuery);
    const tipoFiltroRef = useRef(tipoFiltro);

    useEffect(() => { searchQueryRef.current = searchQuery; }, [searchQuery]);
    useEffect(() => { tipoFiltroRef.current = tipoFiltro; }, [tipoFiltro]);

    const fetchAuditoria = useCallback(async (
        page: number,
        nome: string,
        tipo: TipoFiltro,
        append: boolean,
    ) => {
        if (!append) {
            if (abortControllerRef.current) abortControllerRef.current.abort();
            abortControllerRef.current = new AbortController();
        } else if (isFetchingRef.current) {
            return;
        }

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

            setAuditoriaList(prev => append ? [...prev, ...resultado.items] : resultado.items);
            setCurrentPage(page);
            setHasNextPage(resultado.hasNextPage);
        } catch (err: any) {
            if (axios.isCancel(err)) return;
            if (err?.response?.status === 403) {
                Alert.alert('Acesso negado', 'Você não tem permissão para acessar a auditoria.');
            } else {
                Alert.alert('Erro', 'Não foi possível carregar a auditoria de alertas.');
            }
            console.error(err);
        } finally {
            if (!currentSignal?.aborted) {
                setLoading(false);
                setLoadingMore(false);
                isFetchingRef.current = false;
            }
        }
    }, []);

    useFocusEffect(
        useCallback(() => {
            setAuditoriaList([]);
            setCurrentPage(1);
            fetchAuditoria(1, searchQueryRef.current, tipoFiltroRef.current, false);
        }, [fetchAuditoria])
    );

    const handleSearchChange = useCallback((texto: string) => {
        setSearchQuery(texto);
        if (debounceTimer.current) clearTimeout(debounceTimer.current);
        debounceTimer.current = setTimeout(() => {
            setAuditoriaList([]);
            setCurrentPage(1);
            fetchAuditoria(1, texto, tipoFiltroRef.current, false);
        }, DEBOUNCE_MS);
    }, [fetchAuditoria]);

    const handleTipoChange = useCallback((novoTipo: TipoFiltro) => {
        if (novoTipo === tipoFiltroRef.current) return;
        setTipoFiltro(novoTipo);
        setAuditoriaList([]);
        setCurrentPage(1);
        fetchAuditoria(1, searchQueryRef.current, novoTipo, false);
    }, [fetchAuditoria]);

    const handleEndReached = useCallback(() => {
        if (!hasNextPage || isFetchingRef.current || loading) return;
        fetchAuditoria(currentPage + 1, searchQueryRef.current, tipoFiltroRef.current, true);
    }, [hasNextPage, currentPage, loading, fetchAuditoria]);

    const renderItem = useCallback(
        ({ item }: { item: AuditoriaAlertaDto }) => <AuditoriaCard item={item} />,
        []
    );

    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" />
                <Text variant="labelSmall" style={styles.footerText}>Carregando mais...</Text>
            </View>
        );
    };

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader title="Auditoria de Alertas" onBack={() => navigation.goBack()} />

            {/* Filtros */}
            <View style={styles.filtersContainer}>
                <Searchbar
                    placeholder="Buscar por nome do aluno..."
                    value={searchQuery}
                    onChangeText={handleSearchChange}
                    style={styles.searchBar}
                    inputStyle={styles.searchInput}
                />

                <View style={styles.tipoFilterRow}>
                    {FILTROS_TIPO.map(({ label, valor, icon }) => {
                        const ativo = tipoFiltro === valor;
                        return (
                            <Chip
                                key={valor}
                                selected={ativo}
                                showSelectedOverlay
                                icon={icon as any}
                                onPress={() => handleTipoChange(valor)}
                                style={[styles.filterChip, ativo && styles.filterChipActive]}
                                textStyle={[styles.filterChipText, ativo && styles.filterChipTextActive]}
                            >
                                {label}
                            </Chip>
                        );
                    })}
                </View>
            </View>

            {/* Lista */}
            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" />
                    <Text variant="bodyMedium" style={styles.loadingText}>Carregando auditoria...</Text>
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
                    ListEmptyComponent={
                        <EmptyState
                            icon="archive-outline"
                            title={searchQuery.trim()
                                ? `Nenhum resultado para "${searchQuery}"`
                                : 'Nenhum alerta resolvido no histórico'}
                        />
                    }
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

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },

    // Filtros
    filtersContainer: {
        backgroundColor: theme.colors.surface,
        paddingHorizontal: theme.spacing.md,
        paddingTop: theme.spacing.sm,
        paddingBottom: theme.spacing.sm,
        borderBottomWidth: 1,
        borderBottomColor: theme.colors.border,
        gap: theme.spacing.sm,
    },
    searchBar: {
        backgroundColor: theme.colors.background,
        borderRadius: theme.borderRadius.md,
        elevation: 0,
    },
    searchInput: { fontSize: 15 },
    tipoFilterRow: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
    },
    filterChip: {
        flex: 1,
        backgroundColor: theme.colors.background,
    },
    filterChipActive: {
        backgroundColor: theme.colors.primaryLight,
    },
    filterChipText: {
        fontSize: 12,
        fontWeight: '600',
    },
    filterChipTextActive: {
        color: theme.colors.primary,
        fontWeight: '700',
    },

    // Loading / Empty
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { color: theme.colors.textSecondary, marginTop: theme.spacing.md },
    footerLoader: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
        paddingVertical: theme.spacing.md, gap: theme.spacing.sm,
    },
    footerText: { color: theme.colors.textSecondary },

    // Lista
    listContainer: { padding: theme.spacing.md, paddingBottom: theme.spacing.xxl },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },

    // Card
    card: {
        backgroundColor: theme.colors.surface,
        padding: theme.spacing.md,
        borderRadius: theme.borderRadius.md,
        marginBottom: theme.spacing.sm + 4,
        borderLeftWidth: 5,
    },
    cardHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: theme.spacing.sm,
        gap: theme.spacing.sm,
    },
    cardTitleRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.xs,
        flex: 1,
    },
    cardTitle: { fontWeight: 'bold', flex: 1 },
    cardMeta: {
        flexDirection: 'row',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.sm,
        flexWrap: 'wrap',
    },
    metaChip: {
        backgroundColor: theme.colors.background,
    },
    metaChipText: {
        fontSize: 12,
        color: theme.colors.textSecondary,
    },

    // Auditoria block
    auditBlock: {
        marginTop: theme.spacing.sm,
        paddingTop: theme.spacing.sm,
        borderTopWidth: 1,
        borderTopColor: theme.colors.border,
    },
    auditHeaderRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: theme.spacing.sm,
    },
    auditIdentRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.xs,
        flex: 1,
        marginRight: theme.spacing.sm,
    },
    auditIdentText: {
        color: theme.colors.textSecondary,
    },
    auditNomeUser: {
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
    },
    auditDate: {
        color: theme.colors.textMuted,
    },
    auditJustificativaBox: {
        flexDirection: 'row',
        backgroundColor: theme.colors.background,
        padding: theme.spacing.sm,
        borderRadius: theme.borderRadius.sm,
        borderLeftWidth: 3,
        borderLeftColor: theme.colors.border,
        gap: theme.spacing.xs,
    },
    auditJustificativaText: {
        flex: 1,
        color: theme.colors.textPrimary,
        fontStyle: 'italic',
        lineHeight: 18,
    },
});
