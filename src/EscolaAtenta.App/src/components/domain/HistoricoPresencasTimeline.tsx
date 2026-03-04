import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ActivityIndicator, FlatList } from 'react-native';
import { HistoricoPresencaDto } from '../../types/dtos';
import { alunosService } from '../../services/alunosService';
import { theme } from '../../theme/colors';

interface HistoricoPresencasTimelineProps {
    alunoId: string;
}

export function HistoricoPresencasTimeline({ alunoId }: HistoricoPresencasTimelineProps) {
    const [historico, setHistorico] = useState<HistoricoPresencaDto[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        carregarHistorico();
    }, [alunoId]);

    const carregarHistorico = async () => {
        try {
            setLoading(true);
            const data = await alunosService.obterHistoricoPresencas(alunoId);
            setHistorico(data);
        } catch (error) {
            console.error('Erro ao carregar histórico de presenças:', error);
        } finally {
            setLoading(false);
        }
    };

    const getStatusStyle = (status: string) => {
        switch (status) {
            case 'Presente':
                return { backgroundColor: '#D1FAE5', color: theme.colors.secondary, label: 'Presente' };
            case 'Atraso':
                return { backgroundColor: '#FEF3C7', color: '#92400E', label: 'Atraso' };
            case 'Falta':
                return { backgroundColor: '#FEE2E2', color: theme.colors.error, label: 'Falta' };
            case 'FaltaJustificada':
                return { backgroundColor: '#E0E7FF', color: theme.colors.primary, label: 'Justificada' };
            default:
                return { backgroundColor: theme.colors.background, color: theme.colors.textSecondary, label: status };
        }
    };

    const formatarData = (dataUtcIso: string) => {
        try {
            // Conversão segura UTC -> Fuso Local
            const data = new Date(dataUtcIso);
            return new Intl.DateTimeFormat('pt-BR', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            }).format(data);
        } catch {
            return dataUtcIso;
        }
    };

    if (loading) {
        return (
            <View style={styles.loadingContainer}>
                <ActivityIndicator size="small" color={theme.colors.primary} />
                <Text style={styles.loadingText}>Carregando histórico...</Text>
            </View>
        );
    }

    if (historico.length === 0) {
        return (
            <View style={styles.emptyContainer}>
                <Text style={styles.emptyText}>Nenhum registro de presença encontrado.</Text>
            </View>
        );
    }

    return (
        <View style={styles.container}>
            <Text style={styles.title}>Histórico de Chamadas</Text>
            <FlatList
                data={historico}
                keyExtractor={(item, index) => `${item.dataDaChamada}-${index}`}
                scrollEnabled={false} // Para permitir rolagem junto com a ScrollView superior se houver
                renderItem={({ item }) => {
                    const statusConfig = getStatusStyle(item.status);
                    return (
                        <View style={styles.timelineItem}>
                            <View style={[styles.timelineDot, { backgroundColor: statusConfig.color }]} />
                            <View style={styles.timelineContent}>
                                <Text style={styles.dateText}>{formatarData(item.dataDaChamada)}</Text>
                                <View style={[styles.badge, { backgroundColor: statusConfig.backgroundColor }]}>
                                    <Text style={[styles.badgeText, { color: statusConfig.color }]}>
                                        {statusConfig.label}
                                    </Text>
                                </View>
                            </View>
                        </View>
                    );
                }}
            />
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        marginTop: 24,
        paddingTop: 16,
        borderTopWidth: 1,
        borderColor: theme.colors.border,
    },
    title: {
        fontSize: 16,
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
        marginBottom: 16,
    },
    loadingContainer: {
        marginTop: 24,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
    },
    loadingText: {
        marginLeft: 8,
        color: theme.colors.textSecondary,
        fontSize: 14,
    },
    emptyContainer: {
        marginTop: 24,
        padding: 16,
        backgroundColor: theme.colors.background,
        borderRadius: 8,
        alignItems: 'center',
    },
    emptyText: {
        color: theme.colors.textSecondary,
        fontSize: 14,
    },
    timelineItem: {
        flexDirection: 'row',
        alignItems: 'flex-start',
        marginBottom: 16,
    },
    timelineDot: {
        width: 10,
        height: 10,
        borderRadius: 5,
        marginTop: 6,
        marginRight: 12,
    },
    timelineContent: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        flex: 1,
        backgroundColor: theme.colors.surface,
        padding: 12,
        borderRadius: 8,
        borderWidth: 1,
        borderColor: theme.colors.border,
    },
    dateText: {
        fontSize: 14,
        color: theme.colors.textPrimary,
    },
    badge: {
        paddingHorizontal: 8,
        paddingVertical: 4,
        borderRadius: 12,
    },
    badgeText: {
        fontSize: 12,
        fontWeight: 'bold',
    },
});
