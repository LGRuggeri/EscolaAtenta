import React from 'react';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { ActivityIndicator, View } from 'react-native';
import { useAuth } from '../hooks/useAuth';

// Telas
import { LoginScreen } from '../screens/auth/LoginScreen';
import { HomeScreen } from '../screens/dashboard/HomeScreen';
import { TurmasScreen } from '../screens/gestao/TurmasScreen';
import { TurmaFormScreen } from '../screens/gestao/TurmaFormScreen';
import { AlunosScreen } from '../screens/gestao/AlunosScreen';
import { AlunoFormScreen } from '../screens/gestao/AlunoFormScreen';
import { ChamadaScreen } from '../screens/operacao/ChamadaScreen';
import { AlertasScreen } from '../screens/dashboard/AlertasScreen';
import { RootStackParamList } from './types';

const Stack = createNativeStackNavigator<RootStackParamList>();

export function AppNavigator() {
    const { signed, loading } = useAuth();

    if (loading) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#D4AF37" />
            </View>
        );
    }

    return (
        <Stack.Navigator screenOptions={{ headerShown: false }}>
            {signed ? (
                // Fluxo Logado
                <>
                    <Stack.Screen name="Home" component={HomeScreen} />
                    <Stack.Screen name="Turmas" component={TurmasScreen} />
                    <Stack.Screen name="TurmaForm" component={TurmaFormScreen} />
                    <Stack.Screen name="Alunos" component={AlunosScreen} />
                    <Stack.Screen name="AlunoForm" component={AlunoFormScreen} />
                    <Stack.Screen name="ChamadaOperacao" component={ChamadaScreen} />
                    <Stack.Screen name="Alertas" component={AlertasScreen} />
                </>
            ) : (
                // Fluxo Deslogado
                <Stack.Screen name="Login" component={LoginScreen} />
            )}
        </Stack.Navigator>
    );
}
