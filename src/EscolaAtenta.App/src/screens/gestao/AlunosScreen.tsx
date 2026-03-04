import React, { useState, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, ActivityIndicator, TouchableOpacity, Alert } from 'react-native';
import { useNavigation, useRoute, RouteProp, useFocusEffect } from '@react-navigation/native';
import { AppNavigationProp, RootStackParamList } from '../../navigation/types';
import { AlunoDto } from '../../types/dtos';
import { alunosService } from '../../services/alunosService';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { theme } from '../../theme/colors';

type AlunosRouteProp = RouteProp<RootStackParamList, 'Alunos'>;

export function AlunosScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const route = useRoute<AlunosRouteProp>();
    const { turmaId, turmaNome } = route.params;
    const insets = useSafeAreaInsets();

    const [alunos, setAlunos] = useState<AlunoDto[]>([]);
    const [loading, setLoading] = useState(true);

    useFocusEffect(
        useCallback(() => {
            carregarAlunos();
        }, [turmaId])
    );

    async function carregarAlunos() {
        try {
            setLoading(true);
            const data = await alunosService.obterPorTurma(turmaId);
            setAlunos(data);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar os alunos desta turma.');
            console.error(error);
        } finally {
            setLoading(false);
        }
    }

    const renderItem = ({ item }: { item: AlunoDto }) => (
        <TouchableOpacity
            style={styles.card}
            onPress={() => navigation.navigate('AlunoForm', { turmaId, aluno: item })}
        >
            <View style={styles.cardHeader}>
                <View>
                    <Text style={styles.cardTitle}>{item.nome}</Text>
                    <Text style={styles.cardSubtitle}>Matrícula: {item.matricula || 'N/A'}</Text>
                </View>
                <View style={styles.estatisticasContainer}>
                    <Text style={styles.statsText}>🔴 Seq. Atual: {item.faltasConsecutivasAtuais}</Text>
                    <Text style={styles.statsText}>📅 Faltas (Trimestre): {item.faltasNoTrimestre}</Text>
                    <Text style={styles.statsText}>⏰ Atrasos (Trimestre): {item.atrasosNoTrimestre}</Text>
                </View>
            </View>
        </TouchableOpacity>
    );

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <View style={styles.headerLeft}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                        <Text style={styles.backButtonText}>← Voltar</Text>
                    </TouchableOpacity>
                    <View>
                        <Text style={styles.headerTitle}>Alunos</Text>
                        <Text style={styles.headerSubtitle}>{turmaNome}</Text>
                    </View>
                </View>
                <TouchableOpacity
                    style={styles.addButton}
                    onPress={() => navigation.navigate('AlunoForm', { turmaId })}
                >
                    <Text style={styles.addButtonText}>+ Novo</Text>
                </TouchableOpacity>
            </View>

            {loading ? (
                <ActivityIndicator size="large" color={theme.colors.primary} style={{ marginTop: 50 }} />
            ) : (
                <FlatList
                    data={alunos}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                    ListEmptyComponent={<Text style={styles.emptyText}>Nenhum aluno cadastrado nesta turma.</Text>}
                />
            )}

            <View style={[styles.fabWrapper, { bottom: Math.max(insets.bottom + 30, 40) }]}>
                <TouchableOpacity
                    style={styles.fab}
                    onPress={() => navigation.navigate('ChamadaOperacao', { turmaId, turmaNome })}
                >
                    <Text style={styles.fabText}>📋 Fazer Chamada Hoje</Text>
                </TouchableOpacity>
            </View>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', padding: 20, paddingTop: 20, backgroundColor: theme.colors.surface, elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    headerLeft: { flexDirection: 'row', alignItems: 'center' },
    backButton: { marginRight: 12 },
    backButtonText: { fontSize: 16, color: theme.colors.primary, fontWeight: '600' },
    headerTitle: { fontSize: 20, fontWeight: 'bold', color: theme.colors.textPrimary },
    headerSubtitle: { fontSize: 12, color: theme.colors.textSecondary },
    addButton: { backgroundColor: theme.colors.primary, paddingHorizontal: 16, paddingVertical: 8, borderRadius: 20 },
    addButtonText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 14 },
    listContainer: { padding: 16, paddingBottom: 40 },
    card: { backgroundColor: theme.colors.surface, padding: 20, borderRadius: 12, marginBottom: 12, elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    cardTitle: { fontSize: 16, fontWeight: 'bold', color: theme.colors.textPrimary },
    cardSubtitle: { fontSize: 12, color: theme.colors.textSecondary, marginTop: 4 },
    estatisticasContainer: { marginTop: 8, padding: 8, backgroundColor: theme.colors.background, borderRadius: 8 },
    statsText: { fontSize: 12, color: theme.colors.textSecondary, marginBottom: 2 },
    emptyText: { textAlign: 'center', color: theme.colors.textSecondary, marginTop: 32, fontSize: 16 },
    fabWrapper: { position: 'absolute', left: 0, right: 0, alignItems: 'center' },
    fab: { backgroundColor: theme.colors.secondary, paddingHorizontal: 24, paddingVertical: 14, borderRadius: 30, elevation: 5, shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.25, shadowRadius: 3.84, flexDirection: 'row', alignItems: 'center' },
    fabText: { color: theme.colors.surface, fontWeight: 'bold', fontSize: 16 }
});
