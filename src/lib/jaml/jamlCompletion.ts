import { JAML_KEYWORDS } from './jamlData';

export interface CompletionData {
    text: string;
    displayText: string;
    type: 'keyword' | 'value';
}

export interface YamlCompletionContext {
    key?: string;
    indent: number;
}

export class JamlCompletionService {
    static getCompletions(currentValue: string): CompletionData[] {
        const lower = currentValue.toLowerCase();
        return JAML_KEYWORDS
            .filter(k => k.toLowerCase().includes(lower))
            .map(k => ({
                text: k,
                displayText: k,
                type: 'keyword'
            }));
    }
}
