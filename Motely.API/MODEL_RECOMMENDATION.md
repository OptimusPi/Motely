# Cloudflare Workers AI Model Recommendation

## Current Model: `@cf/meta/llama-3.1-8b-instruct-fp8`

**Why this model is better than the regular 8B:**

### 1. **Much Larger Context Window**
- **FP8**: 32,000 tokens
- **Regular 8B**: 7,968 tokens
- **Why it matters**: Our system prompt is very large (includes complete item catalog, examples, rules). The FP8 version can handle the full prompt without truncation.

### 2. **Better Pricing** (when beta ends)
- **FP8**: $0.15 per M input tokens, $0.29 per M output tokens
- **Regular 8B**: $0.28 per M input tokens, $0.83 per M output tokens
- **Savings**: ~46% cheaper on input, ~65% cheaper on output

### 3. **Still Free in Beta**
- Both models are currently free during beta phase
- FP8 is the better choice for when pricing kicks in

### 4. **Same Capabilities**
- Same instruction-following quality
- Same structured output support
- Same function calling support

## Alternative Models (if FP8 doesn't work well)

### `@cf/meta/llama-3.1-70b-instruct` (if available)
- **Pros**: Much larger model, better reasoning
- **Cons**: Likely not free, slower, more expensive
- **Use case**: If 8B models aren't accurate enough

### `@cf/mistral/mistral-7b-instruct-v0.2`
- **Pros**: Good instruction following, may be free
- **Cons**: Need to verify availability and pricing
- **Use case**: Alternative if Llama models have issues

## How to Change Models

1. **In `appsettings.json`**:
   ```json
   "Cloudflare": {
     "WorkersAI": {
       "Model": "@cf/meta/llama-3.1-8b-instruct-fp8"
     }
   }
   ```

2. **In Cloudflare Worker**:
   Update the model ID in the `ai.run()` call:
   ```javascript
   const response = await ai.run('@cf/meta/llama-3.1-8b-instruct-fp8', {
     messages: [...]
   });
   ```

## Testing

After changing models, test with:
- "hanging chad" → Should generate JAML with `type: Joker` (NOT voucher)
- "telescope" → Should generate JAML with `type: Voucher`
- Complex queries with multiple items

## References

- [Llama 3.1 8B Instruct FP8 Documentation](https://developers.cloudflare.com/workers-ai/models/llama-3.1-8b-instruct-fp8/)
- [Cloudflare Workers AI Models Catalog](https://developers.cloudflare.com/workers-ai/models/)

