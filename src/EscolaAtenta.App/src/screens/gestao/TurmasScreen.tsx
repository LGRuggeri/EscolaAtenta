import React, { useState, useCallback } from 'react';
import { View, StyleSheet, FlatList, Alert } from 'react-native';
import { Text, FAB, ActivityIndicator, Card, IconButton } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppNavigationProp } from '../../navigation/types';
import { TurmaDto } from '../../types/dtos';
import { turmasService } from '../../services/turmasService';
import { AppHeader, EmptyState } from '../../components/ui';
import { theme } from '../../theme/colors';

export function TurmasScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const [turmas, setTurmas] = useState<TurmaDto[]>([]);
    const [loading, setLoading] = useState(true);

    useFocusEffect(
        useCallback(() => {
            carregarTurmas();
        }, [])
    );

    async function carregarTurmas() {
        try {
            setLoading(true);
            const data = await turmasService.obterTodas();
            setTurmas(data);
        } catch (error) {
            Alert.alert('Erro', 'Não foi possível carregar as turmas.');
            console.error(error);
        } finally {
            setLoading(false);
        }
    }

    const renderItem = ({ item }: { item: TurmaDto }) => (
        <Card
            style={styles.card}
            mode="elevated"
            onPress={() => navigation.navigate('Alunos', { turmaId: item.id, turmaNome: item.nome })}
        >
            <Card.Content style={styles.cardContent}>
                <View style={styles.cardLeft}>
                    <View style={styles.iconContainer}>
                        <MaterialCommunityIcons name="google-classroom" size={24} color={theme.colors.primary} />
                    </View>
                    <View style={styles.cardInfo}>
                        <Text variant="titleMedium" style={styles.cardTitle}>{item.nome}</Text>
                        <Text variant="bodySmall" style={styles.cardSubtitle}>
                            {item.turno} · {item.anoLetivo}
                        </Text>
                    </View>
                </View>
                <IconButton
                    icon="pencil-outline"
                    size={20}
                    iconColor={theme.colors.textSecondary}
                    onPress={() => navigation.navigate('TurmaForm', { turma: item })}
                    style={styles.editButton}
                />
            </Card.Content>
        </Card>
    );

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader
                title="Turmas"
                onBack={() => navigation.goBack()}
            />

            {loading ? (
                <View style={styles.loadingContainer}>
                    <ActivityIndicator size="large" />
                </View>
            ) : (
                <FlatList
                    data={turmas}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                    ListEmptyComponent={
                        <EmptyState
                            icon="school-outline"
                            title="Nenhuma turma encontrada"
                            subtitle="Toque no + para criar a primeira turma"
                        />
                    }
                />
            )}

            <FAB
                icon="plus"
                style={styles.fab}
                onPress={() => navigation.navigate('TurmaForm', {})}
                label="Nova Turma"
            />
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    listContainer: { padding: theme.spacing.md, paddingBottom: 100 },
    card: {
        marginBottom: theme.spacing.sm + 4,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.md,
    },
    cardContent: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    cardLeft: {
        flexDirection: 'row',
        alignItems: 'center',
        flex: 1,
    },
    iconContainer: {
        width: 44,
        height: 44,
        borderRadius: theme.borderRadius.sm,
        backgroundColor: theme.colors.primary + '12',
        alignItems: 'center',
        justifyContent: 'center',
        marginRight: theme.spacing.md,
    },
    cardInfo: { flex: 1 },
    cardTitle: { fontWeight: 'bold', color: theme.colors.textPrimary },
    cardSubtitle: { color: theme.colors.textSecondary, marginTop: 2 },
    editButton: { margin: 0 },
    fab: {
        position: 'absolute',
        right: theme.spacing.md,
        bottom: theme.spacing.lg,
        backgroundColor: theme.colors.primary,
        borderRadius: theme.borderRadius.lg,
    },
});
