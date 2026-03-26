import React from 'react';
import { Appbar } from 'react-native-paper';
import { theme } from '../../theme/colors';

interface AppHeaderProps {
    title: string;
    subtitle?: string;
    onBack?: () => void;
    rightActions?: Array<{ icon: string; onPress: () => void; label?: string }>;
}

export function AppHeader({ title, subtitle, onBack, rightActions }: AppHeaderProps) {
    return (
        <Appbar.Header
            style={{ backgroundColor: theme.colors.surface }}
            elevated
        >
            {onBack && <Appbar.BackAction onPress={onBack} />}
            <Appbar.Content
                title={title}
                titleStyle={{ fontWeight: 'bold', color: theme.colors.textPrimary, fontSize: 20 }}
                subtitle={subtitle}
                subtitleStyle={{ color: theme.colors.textSecondary, fontSize: 14 }}
            />
            {rightActions?.map((action, idx) => (
                <Appbar.Action
                    key={idx}
                    icon={action.icon}
                    onPress={action.onPress}
                    accessibilityLabel={action.label}
                />
            ))}
        </Appbar.Header>
    );
}
