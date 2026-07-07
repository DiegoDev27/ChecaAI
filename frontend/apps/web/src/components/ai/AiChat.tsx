'use client';

import { useChat } from 'ai/react';
import { useRef, useEffect } from 'react';
import { Sparkles, Send, Loader2, User, RefreshCw } from 'lucide-react';
import { cn } from '@/lib/utils';

const SUGGESTED_QUESTIONS = [
  'O que é a cota parlamentar e como funciona?',
  'Como posso verificar se um deputado votou contra a população?',
  'O que significa uma votação em regime de urgência?',
  'Quais são os maiores gastos dos parlamentares?',
  'Como funciona o processo de aprovação de um projeto de lei?',
  'O que é uma votação por quórum mínimo?',
];

export function AiChat() {
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const { messages, input, handleInputChange, handleSubmit, isLoading, error, reload, setInput } =
    useChat({
      api: '/api/ai/chat',
      initialMessages: [
        {
          id: 'welcome',
          role: 'assistant',
          content:
            'Olá! Sou o assistente de transparência política do ChecaAI. Posso te ajudar a entender votações parlamentares, gastos públicos, propostas legislativas e muito mais.\n\nO que você gostaria de saber sobre a política brasileira?',
        },
      ],
    });

  // Auto-scroll to latest message
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSuggestion = (q: string) => {
    setInput(q);
    setTimeout(() => inputRef.current?.focus(), 50);
  };

  const userMessages = messages.filter((m) => m.role !== 'assistant' || m.id !== 'welcome');
  const showSuggestions = userMessages.length === 0;

  return (
    <div className="flex flex-col bg-white rounded-xl border shadow-sm overflow-hidden" style={{ height: 'calc(100vh - 220px)', minHeight: 480 }}>
      {/* Header */}
      <div className="flex items-center gap-3 px-5 py-4 border-b bg-gradient-to-r from-primary-50 to-white">
        <div className="bg-primary-100 rounded-full p-2">
          <Sparkles className="h-5 w-5 text-primary-700" />
        </div>
        <div>
          <div className="font-semibold text-slate-900 text-sm">ChecaAI — Assistente Político</div>
          <div className="text-xs text-slate-500">Powered by Claude AI · transparência parlamentar</div>
        </div>
        <div className="ml-auto flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse" />
          <span className="text-xs text-slate-500">Online</span>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
        {messages.map((m) => (
          <div
            key={m.id}
            className={cn('flex gap-3', m.role === 'user' ? 'flex-row-reverse' : 'flex-row')}
          >
            {/* Avatar */}
            <div
              className={cn(
                'h-8 w-8 rounded-full flex items-center justify-center flex-shrink-0 mt-0.5',
                m.role === 'user' ? 'bg-primary-600' : 'bg-primary-100',
              )}
            >
              {m.role === 'user' ? (
                <User className="h-4 w-4 text-white" />
              ) : (
                <Sparkles className="h-4 w-4 text-primary-700" />
              )}
            </div>

            {/* Bubble */}
            <div
              className={cn(
                'max-w-[80%] rounded-2xl px-4 py-3 text-sm leading-relaxed',
                m.role === 'user'
                  ? 'bg-primary-600 text-white rounded-tr-sm'
                  : 'bg-slate-100 text-slate-800 rounded-tl-sm',
              )}
            >
              <p className="whitespace-pre-wrap">{m.content}</p>
            </div>
          </div>
        ))}

        {/* Loading indicator */}
        {isLoading && (
          <div className="flex gap-3">
            <div className="h-8 w-8 rounded-full bg-primary-100 flex items-center justify-center flex-shrink-0">
              <Sparkles className="h-4 w-4 text-primary-700" />
            </div>
            <div className="bg-slate-100 rounded-2xl rounded-tl-sm px-4 py-3 flex items-center gap-2">
              <span className="h-2 w-2 rounded-full bg-slate-400 animate-bounce [animation-delay:0ms]" />
              <span className="h-2 w-2 rounded-full bg-slate-400 animate-bounce [animation-delay:150ms]" />
              <span className="h-2 w-2 rounded-full bg-slate-400 animate-bounce [animation-delay:300ms]" />
            </div>
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="flex items-center gap-3 text-sm text-red-600 bg-red-50 border border-red-200 rounded-xl p-3">
            <span>Erro ao gerar resposta.</span>
            <button
              onClick={() => reload()}
              className="flex items-center gap-1 text-red-700 hover:text-red-900 font-medium"
            >
              <RefreshCw className="h-3.5 w-3.5" />
              Tentar novamente
            </button>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Suggested questions */}
      {showSuggestions && (
        <div className="px-5 py-3 border-t bg-slate-50">
          <p className="text-xs text-slate-500 mb-2 font-medium">Perguntas frequentes:</p>
          <div className="flex flex-wrap gap-2">
            {SUGGESTED_QUESTIONS.map((q) => (
              <button
                key={q}
                onClick={() => handleSuggestion(q)}
                className="text-xs bg-white border border-slate-200 text-slate-700 px-3 py-1.5 rounded-full hover:border-primary-400 hover:text-primary-700 transition-colors"
              >
                {q}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Input */}
      <form
        onSubmit={handleSubmit}
        className="flex items-center gap-3 px-4 py-3 border-t bg-white"
      >
        <input
          ref={inputRef}
          value={input}
          onChange={handleInputChange}
          placeholder="Digite sua pergunta sobre política brasileira..."
          className="flex-1 text-sm border border-slate-200 rounded-xl px-4 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent resize-none"
          disabled={isLoading}
          autoComplete="off"
        />
        <button
          type="submit"
          disabled={isLoading || !input.trim()}
          className={cn(
            'h-10 w-10 rounded-xl flex items-center justify-center transition-colors flex-shrink-0',
            isLoading || !input.trim()
              ? 'bg-slate-100 text-slate-400 cursor-not-allowed'
              : 'bg-primary-600 text-white hover:bg-primary-700',
          )}
        >
          {isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Send className="h-4 w-4" />
          )}
        </button>
      </form>
    </div>
  );
}
