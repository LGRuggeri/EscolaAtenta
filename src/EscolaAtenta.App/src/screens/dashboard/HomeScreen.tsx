import React from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Alert } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { useAuth } from '../../hooks/useAuth';
import { AppNavigationProp } from '../../navigation/types';
import { PapelUsuario } from '../../types/enums';
import { QuadroDeHonraFrequencia } from '../../components/dashboard/QuadroDeHonraFrequencia';

export function HomeScreen() {
    const { user, signOut } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.welcomeTitle}>Olá, {user?.nome?.split(' ')[0]}!</Text>
                <Text style={styles.roleText}>{PapelUsuario[user?.papel || 1]}</Text>
            </View>

            <View style={styles.grid}>
                <TouchableOpacity style={[styles.card, { width: '48%' }]} onPress={() => navigation.navigate('Turmas')}>
                    <Text style={styles.cardIcon}>🏫</Text>
                    <Text style={styles.cardText}>Turmas</Text>
                </TouchableOpacity>

                <TouchableOpacity style={[styles.card, { width: '48%' }]} onPress={() => navigation.navigate('Alertas')}>
                    <Text style={styles.cardIcon}>⚠️</Text>
                    <Text style={[styles.cardText, { color: '#EF4444' }]}>Alertas</Text>
                </TouchableOpacity>
            </View>

            <QuadroDeHonraFrequencia />

            <TouchableOpacity style={styles.logoutButton} onPress={signOut}>
                <Text style={styles.logoutText}>Sair da Conta</Text>
            </TouchableOpacity>
        </SafeAreaView >
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: '#F9F9F9', padding: 20, paddingTop: 60, paddingBottom: 20 },
    header: { marginBottom: 40 },
    welcomeTitle: { fontSize: 28, fontWeight: 'bold', color: '#333' },
    roleText: { fontSize: 16, color: '#D4AF37', fontWeight: 'bold', marginTop: 4 },
    grid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between' },
    card: { width: '100%', backgroundColor: '#FFF', padding: 24, borderRadius: 16, alignItems: 'center', marginBottom: 16, elevation: 3, shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.1, shadowRadius: 4 },
    cardIcon: { fontSize: 32, marginBottom: 12 },
    cardText: { fontSize: 16, fontWeight: '600', color: '#333' },
    logoutButton: { marginTop: 'auto', backgroundColor: '#FFF', borderWidth: 1, borderColor: '#E53935', padding: 16, borderRadius: 12, alignItems: 'center' },
    logoutText: { color: '#E53935', fontWeight: 'bold', fontSize: 16 }
});
