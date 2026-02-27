import React, { useState, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, ActivityIndicator, TouchableOpacity, Alert } from 'react-native';
import { useNavigation, useFocusEffect } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { TurmaDto } from '../../types/dtos';
import { turmasService } from '../../services/turmasService';
import { SafeAreaView } from 'react-native-safe-area-context';

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
        <TouchableOpacity
            style={styles.card}
            onPress={() => navigation.navigate('Alunos', { turmaId: item.id, turmaNome: item.nome })}
        >
            <View style={styles.cardHeader}>
                <View>
                    <Text style={styles.cardTitle}>{item.nome}</Text>
                    <Text style={styles.cardSubtitle}>{item.turno} - {item.anoLetivo}</Text>
                </View>
                <TouchableOpacity
                    style={styles.editButton}
                    onPress={() => navigation.navigate('TurmaForm', { turma: item })}
                >
                    <Text style={styles.editButtonText}>✏️ Editar</Text>
                </TouchableOpacity>
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
                    <Text style={styles.headerTitle}>Turmas</Text>
                </View>
                <TouchableOpacity
                    style={styles.addButton}
                    onPress={() => navigation.navigate('TurmaForm', {})}
                >
                    <Text style={styles.addButtonText}>+ Nova</Text>
                </TouchableOpacity>
            </View>

            {loading ? (
                <ActivityIndicator size="large" color="#D4AF37" style={{ marginTop: 50 }} />
            ) : (
                <FlatList
                    data={turmas}
                    keyExtractor={(item) => item.id}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                    ListEmptyComponent={<Text style={styles.emptyText}>Nenhuma turma encontrada.</Text>}
                />
            )}
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: '#F9F9F9' },
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', padding: 20, paddingTop: 20, backgroundColor: '#FFF', elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    headerLeft: { flexDirection: 'row', alignItems: 'center' },
    backButton: { marginRight: 12 },
    backButtonText: { fontSize: 16, color: '#D4AF37', fontWeight: '600' },
    headerTitle: { fontSize: 24, fontWeight: 'bold', color: '#333' },
    addButton: { backgroundColor: '#D4AF37', paddingHorizontal: 16, paddingVertical: 8, borderRadius: 20 },
    addButtonText: { color: '#FFF', fontWeight: 'bold', fontSize: 14 },
    listContainer: { padding: 16, paddingBottom: 40 },
    card: { backgroundColor: '#FFF', padding: 20, borderRadius: 12, marginBottom: 12, elevation: 2, shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.1, shadowRadius: 2 },
    cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    cardTitle: { fontSize: 18, fontWeight: 'bold', color: '#333' },
    cardSubtitle: { fontSize: 14, color: '#666', marginTop: 4 },
    editButton: { padding: 8, backgroundColor: '#F0F0F0', borderRadius: 8 },
    editButtonText: { fontSize: 12, color: '#333' },
    emptyText: { textAlign: 'center', color: '#888', marginTop: 32, fontSize: 16 }
});
