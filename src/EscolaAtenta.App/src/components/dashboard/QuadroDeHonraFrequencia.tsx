import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ActivityIndicator, ScrollView, TouchableOpacity } from 'react-native';
import { TurmaFrequenciaPerfeitaDto } from '../../types/dtos';
import { dashboardService } from '../../services/dashboardService';
import { theme } from '../../theme/colors';

export function QuadroDeHonraFrequencia() {
    const [turmas, setTurmas] = useState<TurmaFrequenciaPerfeitaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [dias, setDias] = useState<number>(30); // 30 por padrão

    const opçõesDePeriodo = [
        { label: 'Últimos 7 dias', value: 7 },
        { label: 'Últimos 30 dias', value: 30 },
        { label: '1º Trimestre', value: 90 }, // Aproximação
    ];

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
        } catch (error: any) {
            console.error('Erro ao buscar turmas para o Quadro de Honra.', error.response?.data || error.message);
        } finally {
            setLoading(false);
        }
    };

    return (
        <View style={styles.container}>
            <Text style={styles.sectionTitle}>🏆 Quadro de Honra: Frequência 100%</Text>

            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterContainer}>
                {opçõesDePeriodo.map(opcao => (
                    <TouchableOpacity
                        key={opcao.value}
                        style={[styles.filterButton, dias === opcao.value && styles.filterButtonActive]}
                        onPress={() => setDias(opcao.value)}
                    >
                        <Text style={[styles.filterText, dias === opcao.value && styles.filterTextActive]}>
                            {opcao.label}
                        </Text>
                    </TouchableOpacity>
                ))}
            </ScrollView>

            {loading ? (
                <View style={styles.stateContainer}>
                    <ActivityIndicator size="small" color={theme.colors.primary} />
                    <Text style={styles.stateText}>Analisando frequência...</Text>
                </View>
            ) : turmas.length === 0 ? (
                <View style={styles.stateContainer}>
                    <Text style={styles.emptyTextIcon}>🎯</Text>
                    <Text style={styles.stateText}>Nenhuma turma atingiu a perfeição neste período.</Text>
                    <Text style={styles.stateSubText}>Incentive os alunos e professores a não faltarem!</Text>
                </View>
            ) : (
                <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.cardsScroll}>
                    {turmas.map(turma => (
                        <View key={turma.turmaId} style={styles.honorCard}>
                            <View style={styles.honorBadge}>
                                <Text style={styles.honorBadgeText}>100%</Text>
                            </View>
                            <Text style={styles.cardIcon}>⭐</Text>
                            <Text style={styles.className}>{turma.nomeTurma}</Text>
                            <Text style={styles.classStats}>Frequência perfeita em {turma.quantidadeAulasMinistradas} aulas ministradas!</Text>
                        </View>
                    ))}
                </ScrollView>
            )}
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        marginTop: 32,
        marginBottom: 16,
    },
    sectionTitle: {
        fontSize: 18,
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
        marginBottom: 12,
    },
    filterContainer: {
        flexDirection: 'row',
        marginBottom: 16,
    },
    filterButton: {
        paddingHorizontal: 16,
        paddingVertical: 8,
        borderRadius: 20,
        backgroundColor: theme.colors.background,
        marginRight: 8,
    },
    filterButtonActive: {
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.primary,
    },
    filterText: {
        fontSize: 14,
        color: theme.colors.textSecondary,
        fontWeight: '500',
    },
    filterTextActive: {
        color: theme.colors.primaryDark,
        fontWeight: 'bold',
    },
    cardsScroll: {
        paddingVertical: 8,
        paddingBottom: 16,
    },
    honorCard: {
        width: 160,
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        padding: 16,
        marginRight: 16,
        alignItems: 'center',
        elevation: 4,
        shadowColor: theme.colors.primary,
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.15,
        shadowRadius: 8,
        borderWidth: 1,
        borderColor: theme.colors.border,
        position: 'relative',
    },
    honorBadge: {
        position: 'absolute',
        top: -8,
        right: -8,
        backgroundColor: theme.colors.primary,
        paddingHorizontal: 8,
        paddingVertical: 4,
        borderRadius: 12,
        elevation: 2,
    },
    honorBadgeText: {
        color: theme.colors.surface,
        fontSize: 10,
        fontWeight: 'bold',
    },
    cardIcon: {
        fontSize: 32,
        marginBottom: 8,
    },
    className: {
        fontSize: 16,
        fontWeight: 'bold',
        color: theme.colors.textPrimary,
        textAlign: 'center',
        marginBottom: 6,
    },
    classStats: {
        fontSize: 11,
        color: theme.colors.textSecondary,
        textAlign: 'center',
        fontStyle: 'italic',
    },
    stateContainer: {
        padding: 24,
        backgroundColor: theme.colors.surface,
        borderRadius: 12,
        alignItems: 'center',
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderStyle: 'dashed',
    },
    stateText: {
        color: theme.colors.textPrimary,
        fontSize: 14,
        fontWeight: '500',
        marginTop: 8,
        textAlign: 'center',
    },
    stateSubText: {
        color: theme.colors.textSecondary,
        fontSize: 12,
        marginTop: 4,
        textAlign: 'center',
    },
    emptyTextIcon: {
        fontSize: 32,
    }
});
