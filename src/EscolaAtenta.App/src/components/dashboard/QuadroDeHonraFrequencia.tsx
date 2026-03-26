import React, { useEffect, useState } from 'react';
import { View, StyleSheet, ScrollView } from 'react-native';
import { Text, Surface, Chip, ActivityIndicator } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { AxiosError } from 'axios';
import { TurmaFrequenciaPerfeitaDto } from '../../types/dtos';
import { dashboardService } from '../../services/dashboardService';
import { theme } from '../../theme/colors';

const OPCOES_PERIODO = [
    { label: '7 dias', value: 7 },
    { label: '30 dias', value: 30 },
    { label: '1 Tri.', value: 90 },
];

export function QuadroDeHonraFrequencia() {
    const [turmas, setTurmas] = useState<TurmaFrequenciaPerfeitaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [dias, setDias] = useState<number>(30);

    useEffect(() => {
        carregarQuadroDeHonra();
    }, [dias]);

    const carregarQuadroDeHonra = async () => {
        try {
            setLoading(true);
            const hoje = new Date();
            const inicio = new Date();
            inicio.setDate(hoje.getDate() - dias);

            const dataInicio = inicio.toISOString();
            const dataFim = hoje.toISOString();

            const data = await dashboardService.obterTurmasFrequenciaPerfeita(dataInicio, dataFim);
            setTurmas(data);
        } catch (err: unknown) {
            let errorMsg = 'Erro desconhecido';
            if (err instanceof Error) {
                errorMsg = err.message;
            } else if (err && typeof err === 'object' && 'isAxiosError' in err) {
                const axiosError = err as AxiosError<{ data?: string; message?: string }>;
                errorMsg = String(axiosError.response?.data || axiosError.message);
            }
            console.error('Erro ao buscar turmas para o Quadro de Honra.', errorMsg);
        } finally {
            setLoading(false);
        }
    };

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <MaterialCommunityIcons name="trophy" size={22} color={theme.colors.warning} />
                <Text variant="titleMedium" style={styles.sectionTitle}>Quadro de Honra: 100%</Text>
            </View>

            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterContainer}>
                {OPCOES_PERIODO.map(opcao => (
                    <Chip
                        key={opcao.value}
                        selected={dias === opcao.value}
                        showSelectedOverlay
                        onPress={() => setDias(opcao.value)}
                        style={[styles.filterChip, dias === opcao.value && styles.filterChipActive]}
                        textStyle={[styles.filterText, dias === opcao.value && styles.filterTextActive]}
                    >
                        {opcao.label}
                    </Chip>
                ))}
            </ScrollView>

            {loading ? (
                <Surface style={styles.stateContainer} elevation={0}>
                    <ActivityIndicator size="small" />
                    <Text variant="bodySmall" style={styles.stateText}>Analisando frequência...</Text>
                </Surface>
            ) : turmas.length === 0 ? (
                <Surface style={styles.stateContainer} elevation={0}>
                    <MaterialCommunityIcons name="target" size={32} color={theme.colors.textSecondary} />
                    <Text variant="bodyMedium" style={styles.stateText}>
                        Nenhuma turma atingiu a perfeição neste período.
                    </Text>
                    <Text variant="bodySmall" style={styles.stateSubText}>
                        Incentive os alunos e professores a não faltarem!
                    </Text>
                </Surface>
            ) : (
                <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.cardsScroll}>
                    {turmas.map(turma => (
                        <Surface key={turma.turmaId} style={styles.honorCard} elevation={2}>
                            <View style={styles.honorBadge}>
                                <Text variant="labelSmall" style={styles.honorBadgeText}>100%</Text>
                            </View>
                            <MaterialCommunityIcons name="star" size={32} color={theme.colors.warning} />
                            <Text variant="titleSmall" style={styles.className}>{turma.nomeTurma}</Text>
                            <Text variant="labelSmall" style={styles.classStats}>
                                Frequência perfeita em {turma.quantidadeAulasMinistradas} aulas ministradas!
                            </Text>
                        </Surface>
                    ))}
                </ScrollView>
            )}
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        marginTop: theme.spacing.xl,
        marginBottom: theme.spacing.md,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.sm,
        marginBottom: theme.spacing.sm,
    },
    sectionTitle: {
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
    },
    filterContainer: {
        flexDirection: 'row',
        marginBottom: theme.spacing.md,
    },
    filterChip: {
        marginRight: theme.spacing.sm,
        backgroundColor: theme.colors.background,
    },
    filterChipActive: {
        backgroundColor: theme.colors.primaryLight,
    },
    filterText: {
        fontSize: 13,
        color: theme.colors.textSecondary,
    },
    filterTextActive: {
        color: theme.colors.primary,
        fontWeight: 'bold',
    },
    cardsScroll: {
        paddingVertical: theme.spacing.sm,
        paddingBottom: theme.spacing.md,
    },
    honorCard: {
        width: 160,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.lg,
        padding: theme.spacing.md,
        marginRight: theme.spacing.md,
        alignItems: 'center',
        borderWidth: 1,
        borderColor: theme.colors.border,
        position: 'relative',
    },
    honorBadge: {
        position: 'absolute',
        top: -8,
        right: -8,
        backgroundColor: theme.colors.primary,
        paddingHorizontal: theme.spacing.sm,
        paddingVertical: 4,
        borderRadius: theme.borderRadius.full,
    },
    honorBadgeText: {
        color: theme.colors.surface,
        fontWeight: 'bold',
    },
    className: {
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
        textAlign: 'center',
        marginTop: theme.spacing.sm,
        marginBottom: theme.spacing.xs,
    },
    classStats: {
        color: theme.colors.textSecondary,
        textAlign: 'center',
        fontStyle: 'italic',
    },
    stateContainer: {
        padding: theme.spacing.lg,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.md,
        alignItems: 'center',
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderStyle: 'dashed',
        gap: theme.spacing.sm,
    },
    stateText: {
        color: theme.colors.textPrimary,
        fontWeight: '500',
        textAlign: 'center',
    },
    stateSubText: {
        color: theme.colors.textSecondary,
        textAlign: 'center',
    },
});
