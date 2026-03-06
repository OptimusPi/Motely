// Simple test to check if module loads
console.log('Starting test...');

import('./index.js')
  .then(module => {
    console.log('Module loaded:', Object.keys(module));
    return module.loadMotely();
  })
  .then(motely => {
    console.log('Motely loaded successfully!');
    return motely.getCapabilities();
  })
  .then(caps => {
    console.log('Capabilities:', caps);
  })
  .catch(err => {
    console.error('Error:', err);
    console.error('Stack:', err.stack);
  });
