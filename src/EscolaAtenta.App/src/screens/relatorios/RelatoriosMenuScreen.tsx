import React from 'react';
import { View, StyleSheet, ScrollView } from 'react-native';
import { Text } from 'react-native-paper';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { AppNavigationProp } from '../../navigation/types';
import { AppHeader, AppCard } from '../../components/ui';
import { theme } from '../../theme/colors';

export function RelatoriosMenuScreen() {
    const navigation = useNavigation<AppNavigationProp>();

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <AppHeader title="Relatórios" onBack={() => navigation.goBack()} />

            <ScrollView contentContainerStyle={styles.content}>
                <Text variant="bodyMedium" style={styles.subtitle}>
                    Selecione o tipo de relatório que deseja visualizar:
                </Text>

                <View style={styles.grid}>
                    <View style={styles.gridItem}>
                        <AppCard
                            title="Por Aluno"
                            subtitle="Histórico de presenças de um aluno específico"
                            icon="account-details"
                            iconColor={theme.colors.primary}
                            onPress={() => navigation.navigate('RelatorioPresencas')}
                        />
                    </View>

                    <View style={styles.gridItem}>
                        <AppCard
                            title="Por Turma"
                            subtitle="Frequência consolidada de uma turma no ano/período letivo"
                            icon="google-classroom"
                            iconColor={theme.colors.secondary}
                            onPress={() => navigation.navigate('RelatorioTurma')}
                        />
                    </View>
                </View>
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    content: { padding: theme.spacing.md, paddingBottom: theme.spacing.xxl },
    subtitle: {
        color: theme.colors.textSecondary,
        marginBottom: theme.spacing.md,
    },
    grid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: theme.spacing.md,
    },
    gridItem: {
        width: '47%',
        flexGrow: 1,
    },
});
