import type { Ai, VectorizeIndex, R2Bucket } from '@cloudflare/workers-types';

export interface Env {
  AI: Ai;
  VECTORIZE: VectorizeIndex;
  JAML_BUCKET: R2Bucket;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    
    // Handle CORS preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, {
        headers: {
          'Access-Control-Allow-Origin': '*',
          'Access-Control-Allow-Methods': 'POST, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type',
        },
      });
    }
    
    // Endpoint: Generate embedding for text
    if (url.pathname === '/embed' && request.method === 'POST') {
      try {
        const { text } = await request.json() as { text: string };
        
        const embedding = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
          text: [text]
        });
        
        return new Response(JSON.stringify({ embedding: (embedding as any).data?.[0] }), {
          headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
        });
      } catch (error: any) {
        return new Response(JSON.stringify({ error: error.message }), { 
          status: 500,
          headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
        });
      }
    }
    
    // Endpoint: Query similar documents
    if (url.pathname === '/query' && request.method === 'POST') {
      try {
        const { query, topK = 5 } = await request.json() as { query: string; topK?: number };
        
        // Embed the query
        const queryEmbedding = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
          text: [query]
        });
        
        // Search Vectorize
        const results = await env.VECTORIZE.query((queryEmbedding as any).data[0], {
          topK,
          returnMetadata: true
        });
        
        return new Response(JSON.stringify({ results: results.matches }), {
          headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
        });
      } catch (error: any) {
        return new Response(JSON.stringify({ error: error.message }), { 
          status: 500,
          headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
        });
      }
    }
    
    // Endpoint: Index a document
    if (url.pathname === '/index' && request.method === 'POST') {
      try {
        const { id, embedding, metadata } = await request.json() as {
          id: string;
          embedding: number[];
          metadata: Record<string, string>;
        };
        
        await env.VECTORIZE.insert([{
          id,
          values: embedding,
          metadata
        }]);
        
        return new Response(JSON.stringify({ success: true, id }), {
          headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
        });
      } catch (error: any) {
        return new Response(JSON.stringify({ error: error.message }), { 
          status: 500,
          headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
        });
      }
    }
    
    return new Response('Not found', { status: 404 });
  }
};
