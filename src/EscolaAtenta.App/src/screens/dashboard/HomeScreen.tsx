import React from 'react';
import { View, Text, StyleSheet, TouchableOpacity, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { useAuth } from '../../hooks/useAuth';
import { AppNavigationProp } from '../../navigation/types';
import { PapelUsuario } from '../../types/enums';
import { QuadroDeHonraFrequencia } from '../../components/dashboard/QuadroDeHonraFrequencia';
import { theme } from '../../theme/colors';

export function HomeScreen() {
    const { user, signOut } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();

    return (
        <SafeAreaView style={styles.container}>
            <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
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
                        <Text style={[styles.cardText, { color: theme.colors.error }]}>Alertas</Text>
                    </TouchableOpacity>

                    {user?.papel === PapelUsuario.Administrador && (
                        <TouchableOpacity style={[styles.card, { width: '48%' }]} onPress={() => navigation.navigate('Usuarios')}>
                            <Text style={styles.cardIcon}>👤</Text>
                            <Text style={styles.cardText}>Usuários</Text>
                        </TouchableOpacity>
                    )}
                </View>

                <QuadroDeHonraFrequencia />

                <TouchableOpacity style={styles.logoutButton} onPress={signOut}>
                    <Text style={styles.logoutText}>Sair da Conta</Text>
                </TouchableOpacity>
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    scrollContent: { padding: 20, paddingTop: 60, paddingBottom: 40, flexGrow: 1 },
    header: { marginBottom: 40 },
    welcomeTitle: { fontSize: 28, fontWeight: 'bold', color: theme.colors.textPrimary },
    roleText: { fontSize: 16, color: theme.colors.primary, fontWeight: 'bold', marginTop: 4 },
    grid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between' },
    card: { width: '100%', backgroundColor: theme.colors.surface, padding: 24, borderRadius: 16, alignItems: 'center', marginBottom: 16, elevation: 3, shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.1, shadowRadius: 4 },
    cardIcon: { fontSize: 32, marginBottom: 12 },
    cardText: { fontSize: 16, fontWeight: '600', color: theme.colors.textPrimary },
    logoutButton: { marginTop: 'auto', backgroundColor: theme.colors.surface, borderWidth: 1, borderColor: theme.colors.error, padding: 16, borderRadius: 12, alignItems: 'center' },
    logoutText: { color: theme.colors.error, fontWeight: 'bold', fontSize: 16 }
});
