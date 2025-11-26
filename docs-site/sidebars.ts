import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const sidebars: SidebarsConfig = {
  tutorialSidebar: [
    {
      type: 'doc',
      id: 'intro',
      label: 'Introduction',
    },
    {
      type: 'category',
      label: 'Getting Started',
      collapsed: false,
      items: [
        'getting-started/quickstart',
        'getting-started/authentication',
        'getting-started/environments',
      ],
    },
    {
      type: 'category',
      label: 'Guides',
      items: [
        'guides/uploading-audio',
        'guides/voice-conversion',
        'guides/webhooks',
        'guides/error-handling',
      ],
    },
    {
      type: 'category',
      label: 'Security',
      items: [
        'security/authentication',
        'security/webhook-signatures',
      ],
    },
  ],
};

export default sidebars;
