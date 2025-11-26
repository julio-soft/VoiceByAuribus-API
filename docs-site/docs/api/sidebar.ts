import type { SidebarsConfig } from "@docusaurus/plugin-content-docs";

const sidebar: SidebarsConfig = {
  apisidebar: [
    {
      type: "doc",
      id: "api/voicebyauribus-api",
    },
    {
      type: "category",
      label: "Voices",
      items: [
        {
          type: "doc",
          id: "api/get-voices",
          label: "List available voice models",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-voice-by-id",
          label: "Get voice model details",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Audio Files",
      items: [
        {
          type: "doc",
          id: "api/create-audio-file",
          label: "Initiate audio file upload",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-audio-files",
          label: "List your audio files",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-audio-file-by-id",
          label: "Get audio file details",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Voice Conversions",
      items: [
        {
          type: "doc",
          id: "api/create-voice-conversion",
          label: "Create voice conversion",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-voice-conversions",
          label: "List your voice conversions",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-voice-conversion-by-id",
          label: "Get voice conversion details",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Webhooks",
      items: [
        {
          type: "doc",
          id: "api/create-webhook-subscription",
          label: "Create webhook subscription",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-webhook-subscriptions",
          label: "List your webhook subscriptions",
          className: "api-method get",
        },
      ],
    },
  ],
};

export default sidebar.apisidebar;
