/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  images: {
    // Politicians' photos come from many different government domains
    // (Câmara, Senado, state assemblies via SAPL, TSE, municipal portals, etc.)
    // Allow all for now — restrict to specific domains before production
    remotePatterns: [
      { protocol: 'https', hostname: '**' },
      { protocol: 'http',  hostname: '**' },
    ],
  },
};

module.exports = nextConfig;
