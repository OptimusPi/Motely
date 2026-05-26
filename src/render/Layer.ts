/**
 * Layer class for sprite-based card rendering.
 * Encapsulates the source, position, and dimensions of a sprite layer.
 */
export interface LayerOptions {
    pos: { x: number; y: number };
    name: string;
    order: number;
    source: string;
    rows: number;
    columns: number;
    animated?: boolean;
}

export class Layer {
    pos: { x: number; y: number };
    name: string;
    order: number;
    source: string;
    rows: number;
    columns: number;
    animated: boolean;

    constructor({ pos, name, order, source, rows, columns, animated = false }: LayerOptions) {
        this.pos = pos;
        this.name = name;
        this.order = order;
        this.source = source;
        this.rows = rows;
        this.columns = columns;
        this.animated = animated;
    }
}
