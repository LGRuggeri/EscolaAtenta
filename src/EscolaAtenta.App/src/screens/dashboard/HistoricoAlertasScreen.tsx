import React, { useState, useCallback, useRef } from 'react';
import {
    View,
    Text,
    StyleSheet,
    FlatList,
    ActivityIndicator,
    TouchableOpacity,
    Alert,
} from 'react-native';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { AlertaDto } from '../../types/dtos';
import { TipoAlerta, NivelAlertaFalta, parseNivelAlertaFalta } from '../../types/enums';
import { alertasService } from '../../services/alertasService';

const PAGE_SIZE = 20;

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

function formatData(isoDate: string): string {
    return new Date(isoDate).toLocaleDateString('pt-BR', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
    });
}

function TipoBadge({ tipo }: { tipo: TipoAlerta }) {
    const isAtraso = tipo === TipoAlerta.Atraso;
    return (
        <View style={[styles.tipoBadge, isAtraso ? styles.tipoBadgeAtraso : styles.tipoBadgeFalta]}>
            <Text style={[styles.tipoBadgeText, isAtraso ? styles.tipoBadgeTextAtraso : styles.tipoBadgeTextFalta]}>
                {isAtraso ? '⏱️ Atraso' : '❌ Falta'}
            </Text>
        </View>
    );
}

export function HistoricoAlertasScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const [alertas, setAlertas] = useState<AlertaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadingMore, setLoadingMore] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const [hasNextPage, setHasNextPage] = useState(false);

    const isFetchingRef = useRef(false);

    const carregarAlertas = useCallback(async () => {
        try {
            setLoading(true);
            isFetchingRef.current = true;

            const resultado = await alertasService.obterPaginados({
                apenasNaoResolvidos: false,
                pageNumber: 1,
                pageSize: PAGE_SIZE,
            });

            // Filtro local apenas para garantir na UI, mas a API já retorna todos.
            // Para mostrar só os que já foram resolvidos:
            const apenasResolvidos = resultado.items.filter(a => a.resolvido);

            setAlertas(apenasResolvidos);
            setCurrentPage(1);
            setHasNextPage(resultado.hasNextPage);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar o histórico.');
            console.error(error);
        } finally {
            setLoading(false);
            isFetchingRef.current = false;
        }
    }, []);

    const carregarProximaPagina = useCallback(async () => {
        if (isFetchingRef.current || !hasNextPage || loading) return;

        try {
            isFetchingRef.current = true;
            setLoadingMore(true);
            const nextPage = currentPage + 1;

            const resultado = await alertasService.obterPaginados({
                apenasNaoResolvidos: false,
                pageNumber: nextPage,
                pageSize: PAGE_SIZE,
            });

            const apenasResolvidos = resultado.items.filter(a => a.resolvido);

            setAlertas(prev => [...prev, ...apenasResolvidos]);
            setCurrentPage(nextPage);
            setHasNextPage(resultado.hasNextPage);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar mais do histórico.');
            console.error(error);
        } finally {
            setLoadingMore(false);
            isFetchingRef.current = false;
        }
    }, [hasNextPage, currentPage, loading]);

    useFocusEffect(
        useCallback(() => {
            carregarAlertas();
        }, [carregarAlertas])
    );

    const renderItem = ({ item }: { item: AlertaDto }) => {
        const borderColor = getBorderColor(item);
        const tituloExibicao = getTituloExibicao(item);
        const isAtraso = item.tipo === TipoAlerta.Atraso;

        return (
            <View
                style={[
                    styles.card,
                    { borderLeftColor: borderColor },
                    isAtraso && styles.cardAtraso,
                ]}
                accessibilityRole="text"
                accessibilityLabel={`Alerta resolvido de ${item.nomeAluno}. Resolvido por ${item.resolvidoPorNome || 'Usuário Desconhecido'}`}
            >
                <View style={styles.cardHeader}>
                    <Text style={[styles.cardTitle, { color: borderColor }]} numberOfLines={1}>
                        {tituloExibicao}
                    </Text>
                    <TipoBadge tipo={item.tipo} />
                </View>

                <View style={styles.cardMeta}>
                    <Text style={styles.cardAluno} numberOfLines={1}>{item.nomeAluno}</Text>
                    <Text style={styles.cardTurma} numberOfLines={1}>{item.nomeTurma}</Text>
                </View>

                {/* Bloco de Auditoria */}
                <View style={styles.auditBlock}>
                    <View style={styles.auditHeaderRow}>
                        <Text style={styles.auditIdentificacao}>
                            ✅ Tratado por: <Text style={styles.auditNomeUser}>{item.resolvidoPorNome || 'Sistema'}</Text>
                        </Text>
                        {item.dataResolucao && (
                            <Text style={styles.auditDate}>{formatData(item.dataResolucao)}</Text>
                        )}
                    </View>

                    <View style={styles.auditJustificativaBox}>
                        <Text style={styles.auditJustificativaIcon}>📝</Text>
                        <Text style={styles.auditJustificativaText} selectable>
                            "{item.justificativaResolucao || item.observacaoResolucao || 'Nenhuma justificativa informada.'}"
                        </Text>
                    </View>
                </View>
            </View>
        );
    };

    const renderListFooter = () => {
        if (!loadingMore) return null;
        return (
            <View style={styles.footerLoader}>
                <ActivityIndicator size="small" color="#c9a227" />
                <Text style={styles.footerLoaderText}>Carregando histórico...</Text>
            </View>
        );
    };

    const renderListaVazia = () => (
        <View style={styles.emptyContainer}>
            <Text style={styles.emptyIcon}>🗄️</Text>
            <Text style={styles.emptyText}>Nenhum alerta resolvido no histórico.</Text>
        </View>
    );

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Text style={styles.backButtonText}>← Voltar</Text>
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Auditoria de Alertas</Text>
                <View style={{ width: 60 }} />
            </View>

            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" color="#c9a227" />
                    <Text style={styles.loadingText}>Carregando histórico de auditoria...</Text>
                </View>
            ) : (
                <FlatList
                    data={alertas}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={[
                        styles.listContainer,
                        alertas.length === 0 && styles.listContainerEmpty,
                    ]}
                    ListEmptyComponent={renderListaVazia}
                    ListFooterComponent={renderListFooter}
                    onEndReached={carregarProximaPagina}
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
    header: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
        padding: 20, backgroundColor: COLORS.white,
        elevation: 2, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2,
    },
    backButton: {},
    backButtonText: { fontSize: 16, color: COLORS.textSecondary, fontWeight: '600' },
    headerTitle: { fontSize: 18, fontWeight: 'bold', color: COLORS.textPrimary },
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    loadingText: { marginTop: 12, color: COLORS.textMuted },
    footerLoader: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
        paddingVertical: 16, gap: 8,
    },
    footerLoaderText: { fontSize: 13, color: COLORS.textMuted },
    listContainer: { paddingHorizontal: 16, paddingBottom: 40, paddingTop: 16 },
    listContainerEmpty: { flex: 1, justifyContent: 'center' },
    card: {
        backgroundColor: COLORS.white, padding: 16, borderRadius: 10,
        marginBottom: 12, borderLeftWidth: 5,
        elevation: 2, shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.08, shadowRadius: 3,
    },
    cardAtraso: { backgroundColor: COLORS.indigoLight },
    cardHeader: {
        flexDirection: 'row', justifyContent: 'space-between',
        alignItems: 'center', marginBottom: 8, gap: 8,
    },
    cardTitle: { fontSize: 15, fontWeight: 'bold', flex: 1 },
    cardMeta: { flexDirection: 'row', gap: 6, marginBottom: 12, flexWrap: 'wrap' },
    cardAluno: {
        fontSize: 13, fontWeight: '700', color: COLORS.textSecondary,
        backgroundColor: '#F3F4F6', paddingHorizontal: 8, paddingVertical: 2, borderRadius: 6,
    },
    cardTurma: {
        fontSize: 13, color: COLORS.textMuted,
        backgroundColor: '#F3F4F6', paddingHorizontal: 8, paddingVertical: 2, borderRadius: 6,
    },
    tipoBadge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20, alignSelf: 'flex-start' },
    tipoBadgeFalta: { backgroundColor: COLORS.red },
    tipoBadgeAtraso: { backgroundColor: COLORS.indigoLight },
    tipoBadgeText: { fontSize: 11, fontWeight: '700' },
    tipoBadgeTextFalta: { color: '#B91C1C' },
    tipoBadgeTextAtraso: { color: COLORS.indigo },
    emptyContainer: { alignItems: 'center', paddingVertical: 48 },
    emptyIcon: { fontSize: 48, marginBottom: 16 },
    emptyText: { fontSize: 16, fontWeight: '600', color: COLORS.textMuted, textAlign: 'center' },

    // ── Estilos da Auditoria ────────────────────────────────────────────────
    auditBlock: {
        marginTop: 8,
        paddingTop: 12,
        borderTopWidth: 1,
        borderTopColor: '#F3F4F6'
    },
    auditHeaderRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 8
    },
    auditIdentificacao: {
        fontSize: 13,
        color: COLORS.textSecondary
    },
    auditNomeUser: {
        fontWeight: 'bold',
        color: COLORS.textPrimary
    },
    auditDate: {
        fontSize: 12,
        color: COLORS.textMuted
    },
    auditJustificativaBox: {
        flexDirection: 'row',
        backgroundColor: '#F9FAFB',
        padding: 10,
        borderRadius: 8,
        borderLeftWidth: 3,
        borderLeftColor: '#D1D5DB'
    },
    auditJustificativaIcon: {
        fontSize: 14,
        marginRight: 6,
        marginTop: 2
    },
    auditJustificativaText: {
        flex: 1,
        fontSize: 13,
        color: '#4B5563',
        fontStyle: 'italic',
        lineHeight: 18
    }
});
