import { useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeHighlight from "rehype-highlight";
import { Sparkles, Copy, Check } from "lucide-react";
import type { ChatMessage } from "@/types";

interface MessageBubbleProps {
  message: ChatMessage;
}

function CodeBlock({
  children,
  ...props
}: React.HTMLAttributes<HTMLPreElement>) {
  const preRef = useRef<HTMLPreElement>(null);
  const [copied, setCopied] = useState(false);

  function handleCopy() {
    const text = preRef.current?.innerText ?? "";
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <pre ref={preRef} {...props} className="group relative">
      <button
        onClick={handleCopy}
        className="absolute top-2 right-2 rounded-md bg-surface-800/80 p-1.5 text-surface-400 opacity-0 transition-opacity hover:text-surface-200 group-hover:opacity-100"
      >
        {copied ? (
          <Check className="h-3.5 w-3.5 text-accent" />
        ) : (
          <Copy className="h-3.5 w-3.5" />
        )}
      </button>
      {children}
    </pre>
  );
}

export function MessageBubble({ message }: MessageBubbleProps) {
  if (message.role === "user") {
    return (
      <div className="animate-fade-in flex justify-end">
        <div className="max-w-[80%] rounded-2xl rounded-br-md bg-accent/15 px-4 py-3">
          <p className="whitespace-pre-wrap text-sm text-surface-100">
            {message.text}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="animate-fade-in flex gap-3">
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-surface-800">
        <Sparkles className="h-4 w-4 text-accent" />
      </div>
      <div className="min-w-0 max-w-[85%]">
        {message.isPlaceholder ? (
          <div className="flex items-center gap-1.5 py-3">
            <span
              className="inline-block h-2 w-2 rounded-full bg-accent animate-pulse-dot"
              style={{ animationDelay: "0ms" }}
            />
            <span
              className="inline-block h-2 w-2 rounded-full bg-accent animate-pulse-dot"
              style={{ animationDelay: "200ms" }}
            />
            <span
              className="inline-block h-2 w-2 rounded-full bg-accent animate-pulse-dot"
              style={{ animationDelay: "400ms" }}
            />
          </div>
        ) : (
          <div className="prose-chat text-sm text-surface-200">
            <ReactMarkdown
              remarkPlugins={[remarkGfm]}
              rehypePlugins={[rehypeHighlight]}
              components={{
                pre: ({ children, ...props }) => (
                  <CodeBlock {...props}>{children}</CodeBlock>
                ),
              }}
            >
              {message.text}
            </ReactMarkdown>
            {message.isStreaming && (
              <span className="ml-0.5 inline-block h-4 w-1.5 animate-pulse rounded-sm bg-accent/70" />
            )}
          </div>
        )}
      </div>
    </div>
  );
}
