import React, { useState } from 'react';
import { View, StyleSheet, Alert } from 'react-native';
import { TextInput, Button, Text, Surface } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { theme, palette } from '../../theme/colors';
import { api } from '../../services/api';
import { useAuth } from '../../hooks/useAuth';

export function TrocarSenhaScreen() {
    const { signOut } = useAuth();

    const [novaSenha, setNovaSenha] = useState('');
    const [confirmar, setConfirmar] = useState('');
    const [loading, setLoading] = useState(false);
    const [mostrarSenha, setMostrarSenha] = useState(false);

    async function handleTrocar() {
        if (novaSenha.length < 6) {
            Alert.alert('Atenção', 'A nova senha deve ter pelo menos 6 caracteres.');
            return;
        }
        if (novaSenha !== confirmar) {
            Alert.alert('Atenção', 'As senhas não coincidem.');
            return;
        }

        setLoading(true);
        try {
            await api.put('/auth/trocar-senha', { novaSenha });
            Alert.alert(
                'Senha alterada',
                'Sua senha foi alterada com sucesso. Faça login novamente com a nova senha.',
                [{ text: 'OK', onPress: () => signOut() }]
            );
        } catch {
            Alert.alert('Erro', 'Não foi possível alterar a senha. Tente novamente.');
        } finally {
            setLoading(false);
        }
    }

    return (
        <SafeAreaView style={styles.container} edges={['top', 'bottom']}>
            <View style={styles.content}>
                <View style={styles.iconContainer}>
                    <MaterialCommunityIcons name="shield-lock" size={48} color={theme.colors.primary} />
                </View>

                <Text variant="headlineSmall" style={styles.title}>Troca de Senha Obrigatória</Text>
                <Text variant="bodyMedium" style={styles.subtitle}>
                    Por segurança, defina uma nova senha antes de continuar. A senha temporária do sistema não deve ser mantida.
                </Text>

                <TextInput
                    label="Nova senha"
                    value={novaSenha}
                    onChangeText={setNovaSenha}
                    secureTextEntry={!mostrarSenha}
                    placeholder="Mínimo 6 caracteres"
                    mode="outlined"
                    left={<TextInput.Icon icon="lock-outline" />}
                    right={<TextInput.Icon icon={mostrarSenha ? 'eye-off' : 'eye'} onPress={() => setMostrarSenha(!mostrarSenha)} />}
                    autoFocus
                    style={styles.input}
                />

                <TextInput
                    label="Confirmar nova senha"
                    value={confirmar}
                    onChangeText={setConfirmar}
                    secureTextEntry={!mostrarSenha}
                    placeholder="Repita a nova senha"
                    mode="outlined"
                    left={<TextInput.Icon icon="lock-check" />}
                    style={styles.input}
                />

                <Button
                    mode="contained"
                    onPress={handleTrocar}
                    loading={loading}
                    disabled={loading}
                    icon="key"
                    style={styles.button}
                    contentStyle={styles.buttonContent}
                >
                    Definir Nova Senha
                </Button>

                <Button
                    mode="text"
                    onPress={signOut}
                    disabled={loading}
                    textColor={theme.colors.textSecondary}
                >
                    Cancelar e sair
                </Button>
            </View>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    content: { flex: 1, padding: theme.spacing.lg, justifyContent: 'center' },
    iconContainer: { alignItems: 'center', marginBottom: theme.spacing.lg },
    title: { fontWeight: 'bold', color: theme.colors.textPrimary, marginBottom: theme.spacing.sm, textAlign: 'center' },
    subtitle: { color: theme.colors.textSecondary, lineHeight: 22, marginBottom: theme.spacing.xl, textAlign: 'center' },
    input: { marginBottom: theme.spacing.md, backgroundColor: theme.colors.surface },
    button: { marginTop: theme.spacing.sm, borderRadius: theme.borderRadius.md },
    buttonContent: { paddingVertical: theme.spacing.xs },
});
