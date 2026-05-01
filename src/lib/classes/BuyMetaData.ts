export interface BuyMetaData {
    name?: string;
    ante?: number;
    blind?: string;
    card?: {
        type?: string;
        base?: string | string[];
        [key: string]: any;
    };
    [key: string]: any;
}
