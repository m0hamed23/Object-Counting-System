
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api',
  wsUrl: 'ws://localhost:5000', // This was for Socket.IO, can be removed or kept for other WS services
  signalRHubUrl: 'http://localhost:5000/crowdmonitorhub' // NEW for SignalR
};