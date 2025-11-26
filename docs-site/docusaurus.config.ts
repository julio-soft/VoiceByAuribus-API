import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'VoiceByAuribus API Documentation',
  tagline: 'Voice Model Management and Audio Processing API',
  favicon: 'img/favicon.ico',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://docs.auribus.io',
  // Set the /<baseUrl>/ pathname under which your site is served
  baseUrl: '/',

  // Deployment config
  organizationName: 'auribus',
  projectName: 'voicebyauribus-api-docs',

  onBrokenLinks: 'throw',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: 'docs',
          docItemComponent: "@theme/ApiItem", // OpenAPI integration
        },
        blog: false, // Disable blog
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    [
      'docusaurus-plugin-openapi-docs',
      {
        id: "api",
        docsPluginId: "classic",
        config: {
          voicebyauribus: {
            specPath: "openapi/voicebyauribus-api.yaml",
            outputDir: "docs/api",
            sidebarOptions: {
              groupPathsBy: "tag",
              categoryLinkSource: "tag",
            },
          },
        },
      },
    ],
  ],

  themes: ["docusaurus-theme-openapi-docs"],

  themeConfig: {
    // Replace with your project's social card
    image: 'img/docusaurus-social-card.jpg',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Voice By Auribus',
      logo: {
        alt: 'Auribus Logo',
        src: 'img/logo-dark.svg',
        srcDark: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'tutorialSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          to: '/docs/api/voicebyauribus-api',
          label: 'API Reference',
          position: 'left',
        },
        {
          href: 'https://auribus.io',
          label: 'Main Site',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {
              label: 'Getting Started',
              to: '/docs/getting-started/quickstart',
            },
            {
              label: 'API Reference',
              to: '/docs/api/voicebyauribus-api',
            },
            {
              label: 'Guides',
              to: '/docs/guides/uploading-audio',
            },
          ],
        },
        {
          title: 'Resources',
          items: [
            {
              label: 'Security',
              to: '/docs/security/authentication',
            },
            {
              label: 'Webhook Signatures',
              to: '/docs/security/webhook-signatures',
            },
          ],
        },
        {
          title: 'Auribus',
          items: [
            {
              label: 'Main Website',
              href: 'https://auribus.io',
            },
            {
              label: 'API Endpoint',
              href: 'https://api.auribus.io',
            },
            {
              label: 'Support',
              href: 'mailto:support@auribus.io',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Auribus. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'json', 'yaml', 'http'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
