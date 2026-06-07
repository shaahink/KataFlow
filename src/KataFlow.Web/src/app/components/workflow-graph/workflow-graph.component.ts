import { Component, ElementRef, Input, AfterViewInit, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import * as d3 from 'd3';

@Component({
  selector: 'app-workflow-graph',
  standalone: true,
  template: `<div #container class="w-full h-48 bg-gray-50 rounded border overflow-hidden"><svg #svg class="w-full h-full"></svg></div>`
})
export class WorkflowGraphComponent implements AfterViewInit, OnChanges {
  @ViewChild('container') container!: ElementRef<HTMLDivElement>;
  @ViewChild('svg') svg!: ElementRef<SVGSVGElement>;
  @Input() steps: { name: string; status?: string }[] = [];

  ngAfterViewInit() {
    this.render();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['steps']) this.render();
  }

  private render() {
    if (!this.svg?.nativeElement || !this.steps.length) return;

    const svgEl = this.svg.nativeElement;
    const rect = this.container.nativeElement.getBoundingClientRect();
    const width = rect.width || 600;
    const height = rect.height || 192;
    const padding = { top: 40, bottom: 30, left: 40, right: 40 };
    const nodeWidth = 120;
    const nodeHeight = 48;
    const gap = (width - padding.left - padding.right - nodeWidth * this.steps.length) / (this.steps.length + 1);

    d3.select(svgEl).selectAll('*').remove();

    const svg = d3.select(svgEl).attr('viewBox', `0 0 ${width} ${height}`);

    const statusColors: Record<string, string> = {
      completed: '#22c55e', running: '#3b82f6', pending: '#d1d5db',
      failed: '#ef4444', 'waiting-approval': '#eab308',
      done: '#22c55e', 'in-progress': '#3b82f6'
    };

    this.steps.forEach((step, i) => {
      const x = padding.left + gap + i * (nodeWidth + gap);
      const y = height / 2 - nodeHeight / 2;
      const color = statusColors[step.status || 'pending'] || '#d1d5db';

      if (i > 0) {
        const prevX = padding.left + gap + (i - 1) * (nodeWidth + gap);
        svg.append('line')
          .attr('x1', prevX + nodeWidth).attr('y1', height / 2)
          .attr('x2', x).attr('y2', height / 2)
          .attr('stroke', '#94a3b8').attr('stroke-width', 2)
          .attr('marker-end', 'url(#arrowhead)');
      }

      const group = svg.append('g');

      group.append('rect')
        .attr('x', x).attr('y', y)
        .attr('width', nodeWidth).attr('height', nodeHeight)
        .attr('rx', 6).attr('ry', 6)
        .attr('fill', color).attr('opacity', 0.85)
        .attr('stroke', '#475569').attr('stroke-width', 1);

      if (step.status === 'running') {
        group.append('rect')
          .attr('x', x - 2).attr('y', y - 2)
          .attr('width', nodeWidth + 4).attr('height', nodeHeight + 4)
          .attr('rx', 8).attr('ry', 8)
          .attr('fill', 'none').attr('stroke', '#3b82f6')
          .attr('stroke-width', 2).attr('opacity', 0.6)
          .style('animation', 'pulse 2s infinite');
      }

      group.append('text')
        .attr('x', x + nodeWidth / 2).attr('y', y + nodeHeight / 2)
        .attr('text-anchor', 'middle').attr('dominant-baseline', 'middle')
        .attr('fill', 'white').attr('font-size', '13px').attr('font-weight', 'bold')
        .text(step.name);
    });

    svg.append('defs').append('marker')
      .attr('id', 'arrowhead').attr('markerWidth', 10).attr('markerHeight', 7)
      .attr('refX', 9).attr('refY', 3.5).attr('orient', 'auto')
      .append('polygon').attr('points', '0 0, 10 3.5, 0 7').attr('fill', '#94a3b8');
  }
}
