import React from 'react';
import { StyleSheet, View } from 'react-native';
import { Card, Text } from 'react-native-paper';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { theme } from '../../theme/colors';

interface AppCardProps {
    title: string;
    subtitle?: string;
    icon: keyof typeof MaterialCommunityIcons.glyphMap;
    iconColor?: string;
    onPress?: () => void;
    badge?: string | number;
    badgeColor?: string;
}

export function AppCard({ title, subtitle, icon, iconColor, onPress, badge, badgeColor }: AppCardProps) {
    return (
        <Card style={styles.card} onPress={onPress} mode="elevated">
            <Card.Content style={styles.content}>
                <View style={[styles.iconContainer, { backgroundColor: (iconColor ?? theme.colors.primary) + '15' }]}>
                    <MaterialCommunityIcons
                        name={icon}
                        size={28}
                        color={iconColor ?? theme.colors.primary}
                    />
                </View>
                <Text variant="titleMedium" style={styles.title}>{title}</Text>
                {subtitle && (
                    <Text variant="bodySmall" style={styles.subtitle}>{subtitle}</Text>
                )}
                {badge !== undefined && (
                    <View style={[styles.badge, { backgroundColor: badgeColor ?? theme.colors.error }]}>
                        <Text variant="labelSmall" style={styles.badgeText}>{badge}</Text>
                    </View>
                )}
            </Card.Content>
        </Card>
    );
}

const styles = StyleSheet.create({
    card: {
        backgroundColor: theme.colors.surface,
        borderRadius: theme.borderRadius.lg,
    },
    content: {
        alignItems: 'center',
        paddingVertical: theme.spacing.lg,
    },
    iconContainer: {
        width: 56,
        height: 56,
        borderRadius: theme.borderRadius.md,
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: theme.spacing.sm,
    },
    title: {
        fontWeight: '600',
        color: theme.colors.textPrimary,
        textAlign: 'center',
    },
    subtitle: {
        color: theme.colors.textSecondary,
        textAlign: 'center',
        marginTop: 2,
    },
    badge: {
        position: 'absolute',
        top: 8,
        right: 8,
        minWidth: 22,
        height: 22,
        borderRadius: 11,
        alignItems: 'center',
        justifyContent: 'center',
        paddingHorizontal: 6,
    },
    badgeText: {
        color: theme.colors.surface,
        fontWeight: 'bold',
        fontSize: 11,
    },
});
