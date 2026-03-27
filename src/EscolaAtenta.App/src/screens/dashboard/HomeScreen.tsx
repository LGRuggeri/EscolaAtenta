import React from 'react';
import { View, StyleSheet, ScrollView } from 'react-native';
import { Text, Button, ActivityIndicator } from 'react-native-paper';
import { LinearGradient } from 'expo-linear-gradient';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from '@react-navigation/native';
import { useAuth } from '../../hooks/useAuth';
import { AppNavigationProp } from '../../navigation/types';
import { PapelUsuario } from '../../types/enums';
import { QuadroDeHonraFrequencia } from '../../components/dashboard/QuadroDeHonraFrequencia';
import { AppCard } from '../../components/ui';
import { theme, palette } from '../../theme/colors';
import { useSyncEngine } from '../../hooks/useSyncEngine';

export function HomeScreen() {
    const { user, signOut } = useAuth();
    const navigation = useNavigation<AppNavigationProp>();
    const { isSyncing, temPendentes } = useSyncEngine();

    return (
        <SafeAreaView style={styles.container} edges={['top']}>
            <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
                {/* Header com gradiente */}
                <LinearGradient
                    colors={[palette.navy, palette.navyLight]}
                    style={styles.headerGradient}
                    start={{ x: 0, y: 0 }}
                    end={{ x: 1, y: 1 }}
                >
                    <View style={styles.headerContent}>
                        <View style={styles.headerTop}>
                            <View style={styles.headerLeft}>
                                <Text variant="headlineSmall" style={styles.welcomeTitle}>
                                    Olá, {user?.nome?.split(' ')[0]}!
                                </Text>
                                <Text variant="labelLarge" style={styles.roleText}>
                                    {PapelUsuario[user?.papel || 1]}
                                </Text>
                            </View>
                            <View style={styles.avatarCircle}>
                                <MaterialCommunityIcons name="account" size={28} color={palette.navy} />
                            </View>
                        </View>

                        {/* Sync status */}
                        {isSyncing && (
                            <View style={styles.syncRow}>
                                <ActivityIndicator size={14} color={palette.white} />
                                <Text variant="labelSmall" style={styles.syncText}>Sincronizando...</Text>
                            </View>
                        )}
                        {!isSyncing && temPendentes && (
                            <View style={styles.syncRow}>
                                <MaterialCommunityIcons name="clock-outline" size={14} color={palette.gray300} />
                                <Text variant="labelSmall" style={styles.syncText}>Pendente — aguardando rede</Text>
                            </View>
                        )}
                    </View>
                </LinearGradient>

                {/* Grid de navegação */}
                <View style={styles.gridContainer}>
                    <View style={styles.grid}>
                        <View style={styles.gridItem}>
                            <AppCard
                                title="Turmas"
                                icon="school-outline"
                                iconColor={theme.colors.primary}
                                onPress={() => navigation.navigate('Turmas')}
                            />
                        </View>

                        <View style={styles.gridItem}>
                            <AppCard
                                title="Alertas"
                                icon="alert-circle-outline"
                                iconColor={theme.colors.error}
                                onPress={() => navigation.navigate('Alertas')}
                            />
                        </View>

                        <View style={styles.gridItem}>
                            <AppCard
                                title="Relatório"
                                icon="chart-bar"
                                iconColor={theme.colors.secondary}
                                onPress={() => navigation.navigate('RelatorioPresencas')}
                            />
                        </View>

                        {user?.papel === PapelUsuario.Administrador && (
                            <View style={styles.gridItem}>
                                <AppCard
                                    title="Usuários"
                                    icon="account-group-outline"
                                    iconColor={theme.colors.info}
                                    onPress={() => navigation.navigate('Usuarios')}
                                />
                            </View>
                        )}
                    </View>
                </View>

                {/* Quadro de Honra */}
                <View style={styles.sectionContainer}>
                    <QuadroDeHonraFrequencia />
                </View>

                {/* Logout */}
                <View style={styles.logoutContainer}>
                    <Button
                        mode="outlined"
                        onPress={signOut}
                        icon="logout"
                        textColor={theme.colors.error}
                        style={styles.logoutButton}
                    >
                        Sair da Conta
                    </Button>
                </View>
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: theme.colors.background },
    scrollContent: { flexGrow: 1, paddingBottom: theme.spacing.xxl + 24 },
    headerGradient: {
        paddingTop: theme.spacing.lg,
        paddingBottom: theme.spacing.xl + 8,
        paddingHorizontal: theme.spacing.lg,
        borderBottomLeftRadius: theme.borderRadius.xl,
        borderBottomRightRadius: theme.borderRadius.xl,
    },
    headerContent: {},
    headerTop: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    headerLeft: {},
    welcomeTitle: { color: palette.white, fontWeight: 'bold' },
    roleText: { color: palette.olivePale, marginTop: 2 },
    avatarCircle: {
        width: 48,
        height: 48,
        borderRadius: 24,
        backgroundColor: palette.white,
        alignItems: 'center',
        justifyContent: 'center',
    },
    syncRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        marginTop: theme.spacing.sm,
    },
    syncText: { color: palette.gray300 },
    gridContainer: {
        marginTop: -theme.spacing.md,
        paddingHorizontal: theme.spacing.md,
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
    sectionContainer: {
        paddingHorizontal: theme.spacing.lg,
        marginTop: theme.spacing.lg,
    },
    logoutContainer: {
        paddingHorizontal: theme.spacing.lg,
        marginTop: theme.spacing.xl,
    },
    logoutButton: {
        borderColor: theme.colors.error,
        borderRadius: theme.borderRadius.md,
    },
});
