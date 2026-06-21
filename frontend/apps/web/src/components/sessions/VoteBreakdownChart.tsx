'use client';

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  ResponsiveContainer,
  Cell,
} from 'recharts';

interface PartyRow {
  party: string;
  yes: number;
  no: number;
  abstention: number;
  absent: number;
  total: number;
}

interface Props {
  parties: PartyRow[];
}

const COLORS = {
  yes: '#16a34a',
  no: '#dc2626',
  abstention: '#ea580c',
  absent: '#9ca3af',
};

// Show top 12 parties by total votes (chart gets cluttered beyond that)
const MAX_PARTIES = 12;

export function VoteBreakdownChart({ parties }: Props) {
  const top = parties.slice(0, MAX_PARTIES);

  const data = top.map((p) => ({
    party: p.party,
    Sim: p.yes,
    Não: p.no,
    Abstenção: p.abstention,
    Ausente: p.absent,
  }));

  if (data.length === 0) return null;

  return (
    <div className="w-full h-64">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart
          data={data}
          margin={{ top: 4, right: 8, left: -16, bottom: 0 }}
          barSize={14}
        >
          <XAxis
            dataKey="party"
            tick={{ fontSize: 11, fill: '#6b7280' }}
            tickLine={false}
            axisLine={false}
          />
          <YAxis
            tick={{ fontSize: 11, fill: '#9ca3af' }}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip
            contentStyle={{ fontSize: 12, borderRadius: 8, border: '1px solid #e5e7eb' }}
            cursor={{ fill: '#f3f4f6' }}
          />
          <Legend
            wrapperStyle={{ fontSize: 12, paddingTop: 8 }}
            iconType="circle"
            iconSize={8}
          />
          <Bar dataKey="Sim" stackId="a" fill={COLORS.yes} radius={[0, 0, 0, 0]} />
          <Bar dataKey="Não" stackId="a" fill={COLORS.no} />
          <Bar dataKey="Abstenção" stackId="a" fill={COLORS.abstention} />
          <Bar dataKey="Ausente" stackId="a" fill={COLORS.absent} radius={[3, 3, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
