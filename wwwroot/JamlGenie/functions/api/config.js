// Cloudflare Pages Function - Returns API configuration
export async function onRequest(context) {
  const apiBaseUrl = context.env.API_BASE_URL || '';
  
  return new Response(JSON.stringify({ apiBaseUrl }), {
    headers: {
      'Content-Type': 'application/json',
      'Access-Control-Allow-Origin': '*'
    }
  });
}


