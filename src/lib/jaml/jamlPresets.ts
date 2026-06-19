export interface JamlPreset {
    id: string;
    label: string;
    description: string;
    jaml: string;
    icon?: string;
}

export const JAML_PRESETS: JamlPreset[] = [
    {
        id: 'soul-hunter',
        label: 'Soul Hunter',
        description: 'Find a Spectral Soul card in early Antes.',
        jaml: `# The Soul Hunter
name: Soul Hunter
deck: Red
stake: White

must:
  - spectralCard: The Soul
    antes: [1, 2, 3]
`
    },
    {
        id: 'negative-blueprint',
        label: 'Negative Blueprint',
        description: 'Find a Negative Blueprint early.',
        jaml: `# Negative Blueprint
name: Negative Blueprint
deck: Red
stake: White

must:
  - joker: Blueprint
    edition: Negative
    antes: [1, 2, 3, 4]
`
    },
    {
        id: 'mega-tags',
        label: 'Mega Tags',
        description: 'Find a lot of Investment and Double Tags.',
        jaml: `# Mega Tags
name: Mega Tags
deck: Red
stake: White

must:
  - tag: Double Tag
    antes: [1, 2]
  - tag: Investment Tag
    antes: [1, 2]
`
    },
    {
        id: 'black-hole',
        label: 'Black Hole',
        description: 'Find the elusive Black Hole spectral card.',
        jaml: `# Black Hole
name: Black Hole
deck: Red
stake: White

must:
  - spectralCard: Black Hole
    antes: [1, 2, 3, 4]
`
    }
];
