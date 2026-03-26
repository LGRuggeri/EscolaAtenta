import React, { useEffect, useState } from 'react';
import { View, StyleSheet, FlatList } from 'react-native';
import { Text, Surface, Chip, ActivityIndicator } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { HistoricoPresencaDto } from '../../types/dtos';
import { alunosService } from '../../services/alunosService';
import { theme } from '../../theme/colors';

interface HistoricoPresencasTimelineProps {
    alunoId: string;
}

function getStatusStyle(status: string): { bg: string; color: string; label: string; icon: string } {
    switch (status) {
        case 'Presente':
            return { bg: theme.colors.successLight, color: theme.colors.success, label: 'Presente', icon: 'check-circle' };
        case 'Atraso':
            return { bg: theme.colors.warningLight, color: theme.colors.warning, label: 'Atraso', icon: 'clock-alert' };
        case 'Falta':
            return { bg: theme.colors.errorLight, color: theme.colors.error, label: 'Falta', icon: 'close-circle' };
        case 'FaltaJustificada':
            return { bg: theme.colors.infoLight, color: theme.colors.info, label: 'Justificada', icon: 'file-document-check' };
        default:
            return { bg: theme.colors.background, color: theme.colors.textSecondary, label: status, icon: 'help-circle' };
    }
}

function formatarData(dataUtcIso: string): string {
    try {
        const data = new Date(dataUtcIso);
        return new Intl.DateTimeFormat('pt-BR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
        }).format(data);
    } catch {
        return dataUtcIso;
    }
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

    if (loading) {
        return (
            <View style={styles.loadingContainer}>
                <ActivityIndicator size="small" />
                <Text variant="bodySmall" style={styles.loadingText}>Carregando histórico...</Text>
            </View>
        );
    }

    if (historico.length === 0) {
        return (
            <Surface style={styles.emptyContainer} elevation={0}>
                <MaterialCommunityIcons name="calendar-blank" size={24} color={theme.colors.textSecondary} />
                <Text variant="bodySmall" style={styles.emptyText}>
                    Nenhum registro de presença encontrado.
                </Text>
            </Surface>
        );
    }

    return (
        <View style={styles.container}>
            <View style={styles.titleRow}>
                <MaterialCommunityIcons name="timeline-clock" size={20} color={theme.colors.primary} />
                <Text variant="titleSmall" style={styles.title}>Histórico de Chamadas</Text>
            </View>
            <FlatList
                data={historico}
                keyExtractor={(item, index) => `${item.dataDaChamada}-${index}`}
                scrollEnabled={false}
                renderItem={({ item }) => {
                    const statusConfig = getStatusStyle(item.status);
                    return (
                        <View style={styles.timelineItem}>
                            <View style={[styles.timelineDot, { backgroundColor: statusConfig.color }]} />
                            <Surface style={styles.timelineContent} elevation={0}>
                                <Text variant="bodySmall" style={styles.dateText}>
                                    {formatarData(item.dataDaChamada)}
                                </Text>
                                <Chip
                                    compact
                                    icon={() => (
                                        <MaterialCommunityIcons
                                            name={statusConfig.icon as any}
                                            size={14}
                                            color={statusConfig.color}
                                        />
                                    )}
                                    textStyle={{ fontSize: 11, fontWeight: '700', color: statusConfig.color }}
                                    style={{ backgroundColor: statusConfig.bg }}
                                >
                                    {statusConfig.label}
                                </Chip>
                            </Surface>
                        </View>
                    );
                }}
            />
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        marginTop: theme.spacing.lg,
        paddingTop: theme.spacing.md,
        borderTopWidth: 1,
        borderColor: theme.colors.border,
    },
    titleRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.md,
    },
    title: {
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
    },
    loadingContainer: {
        marginTop: theme.spacing.lg,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: theme.spacing.sm,
    },
    loadingText: {
        color: theme.colors.textSecondary,
    },
    emptyContainer: {
        marginTop: theme.spacing.lg,
        padding: theme.spacing.md,
        backgroundColor: theme.colors.background,
        borderRadius: theme.borderRadius.sm,
        alignItems: 'center',
        gap: theme.spacing.sm,
    },
    emptyText: {
        color: theme.colors.textSecondary,
    },
    timelineItem: {
        flexDirection: 'row',
        alignItems: 'flex-start',
        marginBottom: theme.spacing.md,
    },
    timelineDot: {
        width: 10,
        height: 10,
        borderRadius: 5,
        marginTop: 6,
        marginRight: theme.spacing.sm,
    },
    timelineContent: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        flex: 1,
        backgroundColor: theme.colors.surface,
        padding: theme.spacing.sm,
        borderRadius: theme.borderRadius.sm,
        borderWidth: 1,
        borderColor: theme.colors.border,
    },
    dateText: {
        color: theme.colors.textPrimary,
    },
});
