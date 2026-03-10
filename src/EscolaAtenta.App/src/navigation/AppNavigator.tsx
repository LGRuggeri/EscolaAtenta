import React from 'react';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { ActivityIndicator, View } from 'react-native';
import { useAuth } from '../hooks/useAuth';
import { theme } from '../theme/colors';

// Telas
import { LoginScreen } from '../screens/auth/LoginScreen';
import { TrocarSenhaScreen } from '../screens/auth/TrocarSenhaScreen';
import { HomeScreen } from '../screens/dashboard/HomeScreen';
import { TurmasScreen } from '../screens/gestao/TurmasScreen';
import { TurmaFormScreen } from '../screens/gestao/TurmaFormScreen';
import { AlunosScreen } from '../screens/gestao/AlunosScreen';
import { AlunoFormScreen } from '../screens/gestao/AlunoFormScreen';
import { ChamadaScreen } from '../screens/operacao/ChamadaScreen';
import { UsuariosScreen } from '../screens/gestao/UsuariosScreen';
import { UsuarioFormScreen } from '../screens/gestao/UsuarioFormScreen';
import { AlertasScreen } from '../screens/dashboard/AlertasScreen';
import { HistoricoAlertasScreen } from '../screens/dashboard/HistoricoAlertasScreen';
import { RelatorioPresencasScreen } from '../screens/relatorios/RelatorioPresencasScreen';
import { ConfiguracaoServidorScreen } from '../screens/settings/ConfiguracaoServidorScreen';
import { RootStackParamList } from './types';

const Stack = createNativeStackNavigator<RootStackParamList>();

export function AppNavigator() {
    const { signed, loading, deveAlterarSenha } = useAuth();

    if (loading) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color={theme.colors.primary} />
            </View>
        );
    }

    return (
        <Stack.Navigator screenOptions={{ headerShown: false }}>
            {signed && deveAlterarSenha ? (
                // Fluxo: logado mas precisa trocar senha antes de acessar o sistema
                <>
                    <Stack.Screen name="TrocarSenha" component={TrocarSenhaScreen} />
                </>
            ) : signed ? (
                // Fluxo Logado
                <>
                    <Stack.Screen name="Home" component={HomeScreen} />
                    <Stack.Screen name="Turmas" component={TurmasScreen} />
                    <Stack.Screen name="TurmaForm" component={TurmaFormScreen} />
                    <Stack.Screen name="Alunos" component={AlunosScreen} />
                    <Stack.Screen name="AlunoForm" component={AlunoFormScreen} />
                    <Stack.Screen name="ChamadaOperacao" component={ChamadaScreen} />
                    <Stack.Screen name="Usuarios" component={UsuariosScreen} />
                    <Stack.Screen name="UsuarioForm" component={UsuarioFormScreen} />
                    <Stack.Screen name="Alertas" component={AlertasScreen} />
                    <Stack.Screen name="HistoricoAlertas" component={HistoricoAlertasScreen} />
                    <Stack.Screen name="RelatorioPresencas" component={RelatorioPresencasScreen} />
                    <Stack.Screen name="TrocarSenha" component={TrocarSenhaScreen} />
                </>
            ) : (
                // Fluxo Deslogado
                <>
                    <Stack.Screen name="Login" component={LoginScreen} />
                    <Stack.Screen name="ConfiguracaoServidor" component={ConfiguracaoServidorScreen} />
                </>
            )}
        </Stack.Navigator>
    );
}
