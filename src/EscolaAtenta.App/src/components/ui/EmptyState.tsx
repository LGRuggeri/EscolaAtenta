import React from 'react';
import { StyleSheet, View } from 'react-native';
import { Text } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { theme } from '../../theme/colors';

interface EmptyStateProps {
    icon: keyof typeof MaterialCommunityIcons.glyphMap;
    title: string;
    subtitle?: string;
}

export function EmptyState({ icon, title, subtitle }: EmptyStateProps) {
    return (
        <View style={styles.container}>
            <MaterialCommunityIcons
                name={icon}
                size={56}
                color={theme.colors.textSecondary}
                style={styles.icon}
            />
            <Text variant="titleMedium" style={styles.title}>{title}</Text>
            {subtitle && (
                <Text variant="bodyMedium" style={styles.subtitle}>{subtitle}</Text>
            )}
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        alignItems: 'center',
        paddingVertical: theme.spacing.xxl,
        paddingHorizontal: theme.spacing.lg,
    },
    icon: {
        marginBottom: theme.spacing.md,
        opacity: 0.6,
    },
    title: {
        color: theme.colors.textPrimary,
        fontWeight: '600',
        textAlign: 'center',
    },
    subtitle: {
        color: theme.colors.textSecondary,
        textAlign: 'center',
        marginTop: theme.spacing.xs,
    },
});
