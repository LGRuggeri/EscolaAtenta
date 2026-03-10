import React, { useState } from 'react';
import { View, Text, StyleSheet, TextInput, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { theme } from '../../theme/colors';
import { api } from '../../services/api';
import { useAuth } from '../../hooks/useAuth';

export function TrocarSenhaScreen() {
    const navigation = useNavigation<AppNavigationProp>();
    const { signOut } = useAuth();

    const [novaSenha, setNovaSenha] = useState('');
    const [confirmar, setConfirmar] = useState('');
    const [loading, setLoading] = useState(false);

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
        <SafeAreaView style={styles.container}>
            <View style={styles.content}>
                <Text style={styles.title}>Troca de Senha Obrigatória</Text>
                <Text style={styles.subtitle}>
                    Por segurança, você precisa definir uma nova senha antes de continuar.
                    A senha temporária do sistema não deve ser mantida.
                </Text>

                <Text style={styles.label}>Nova senha</Text>
                <TextInput
                    style={styles.input}
                    value={novaSenha}
                    onChangeText={setNovaSenha}
                    secureTextEntry
                    placeholder="Mínimo 6 caracteres"
                    placeholderTextColor={theme.colors.textSecondary}
                    autoFocus
                />

                <Text style={styles.label}>Confirmar nova senha</Text>
                <TextInput
                    style={styles.input}
                    value={confirmar}
                    onChangeText={setConfirmar}
                    secureTextEntry
                    placeholder="Repita a nova senha"
                    placeholderTextColor={theme.colors.textSecondary}
                />

                <TouchableOpacity
                    style={[styles.button, loading && styles.buttonDisabled]}
                    onPress={handleTrocar}
                    disabled={loading}
                >
                    {loading
                        ? <ActivityIndicator color={theme.colors.surface} />
                        : <Text style={styles.buttonText}>Definir Nova Senha</Text>}
                </TouchableOpacity>

                <TouchableOpacity style={styles.cancelButton} onPress={signOut} disabled={loading}>
                    <Text style={styles.cancelText}>Cancelar e sair</Text>
                </TouchableOpacity>
            </View>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    content: { flex: 1, padding: 24, justifyContent: 'center' },
    title: { fontSize: 22, fontWeight: 'bold', color: theme.colors.textPrimary, marginBottom: 12 },
    subtitle: { fontSize: 14, color: theme.colors.textSecondary, lineHeight: 20, marginBottom: 32 },
    label: { fontSize: 14, fontWeight: '600', color: theme.colors.textSecondary, marginBottom: 6 },
    input: {
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderRadius: 8,
        padding: 14,
        fontSize: 16,
        color: theme.colors.textPrimary,
        marginBottom: 20,
    },
    button: {
        backgroundColor: theme.colors.primary,
        padding: 16,
        borderRadius: 12,
        alignItems: 'center',
        marginTop: 8,
    },
    buttonDisabled: { opacity: 0.7 },
    buttonText: { color: theme.colors.surface, fontSize: 16, fontWeight: 'bold' },
    cancelButton: { marginTop: 16, alignItems: 'center' },
    cancelText: { color: theme.colors.textSecondary, fontSize: 14 },
});
