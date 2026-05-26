export interface BuyMetaData {
    name?: string;
    ante?: number;
    blind?: string;
    card?: {
        type?: string;
        base?: string | string[];
        [key: string]: unknown;
    };
    [key: string]: unknown;
}
