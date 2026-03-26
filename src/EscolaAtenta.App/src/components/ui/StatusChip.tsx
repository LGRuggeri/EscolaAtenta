import React from 'react';
import { Chip } from 'react-native-paper';
import { theme } from '../../theme/colors';

type ChipVariant = 'success' | 'error' | 'warning' | 'info' | 'neutral';

interface StatusChipProps {
    label: string;
    variant?: ChipVariant;
    icon?: string;
    compact?: boolean;
}

const variantStyles: Record<ChipVariant, { bg: string; text: string }> = {
    success: { bg: theme.colors.successLight, text: theme.colors.success },
    error: { bg: theme.colors.errorLight, text: theme.colors.error },
    warning: { bg: theme.colors.warningLight, text: theme.colors.warning },
    info: { bg: theme.colors.infoLight, text: theme.colors.info },
    neutral: { bg: theme.colors.surfaceVariant, text: theme.colors.textSecondary },
};

export function StatusChip({ label, variant = 'neutral', icon, compact = true }: StatusChipProps) {
    const style = variantStyles[variant];
    return (
        <Chip
            icon={icon}
            compact={compact}
            textStyle={{ color: style.text, fontWeight: '600', fontSize: 12 }}
            style={{ backgroundColor: style.bg }}
        >
            {label}
        </Chip>
    );
}
