import type { Meta, StoryObj } from '@storybook/react';
import { SeedMascot } from './SeedMascot';
import { MenuColors } from './menuConfig';
import { RadialBreadcrumb } from '../radial';

const meta = {
    title: 'UI/SeedMascot',
    component: SeedMascot,
    parameters: {
        layout: 'centered',
    },
    tags: ['autodocs'],
} satisfies Meta<typeof SeedMascot>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
    args: {
        size: 160,
    },
};

export const WithRadialMenu: Story = {
    args: {
        size: 160,
        showRadialMenu: true,
        radialItems: [
            { label: 'Action 1', action: 'action1', color: MenuColors.ACTION },
            { label: 'Chat', action: 'chat', color: MenuColors.CHAT },
            { label: 'Settings', action: 'settings', color: MenuColors.SUBMENU },
            { label: 'Back', action: 'back', color: MenuColors.TOGGLE, _south: true },
        ],
    },
};

export const WithManyItems: Story = {
    args: {
        size: 160,
        showRadialMenu: true,
        radialItems: [
            { label: 'Action 1', action: 'action1', color: MenuColors.ACTION },
            { label: 'Action 2', action: 'action2', color: MenuColors.ACTION },
            { label: 'Chat', action: 'chat', color: MenuColors.CHAT },
            { label: 'Submenu 1', action: 'sub1', color: MenuColors.SUBMENU },
            { label: 'Submenu 2', action: 'sub2', color: MenuColors.SUBMENU },
            { label: 'Submenu 3', action: 'sub3', color: MenuColors.SUBMENU },
            { label: 'Back', action: 'back', color: MenuColors.TOGGLE, _south: true },
        ],
        breadcrumb: <RadialBreadcrumb label="Menu" title="Main Menu" />,
    },
};
