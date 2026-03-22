import json
import math
from typing import Optional, Dict, Any
from collections import defaultdict

import matplotlib.pyplot as plt
import numpy as np

def display_deepeval_results(
    json_path: str,
    save_path: Optional[str] = None,
    figsize: tuple = (15, 10)
) -> Dict[str, Any]:
    """
    Display DeepEval test results.
    
    Args:
        json_path: Path to the json file
        save_path: Optional path to save the figure (e.g., 'results.png')
        figsize: Figure size as (width, height) tuple
    
    Returns:
        Dictionary containing the parsed JSON data
    """
    
    # Load the JSON data
    try:
        with open(json_path, 'r') as f:
            data = json.load(f)
            filename = json_path.split("\\")[-1]
    except FileNotFoundError:
        print(f"Error: Could not find {json_path}")
        return {}
    except json.JSONDecodeError:
        print(f"Error: {json_path} contains invalid JSON")
        return {}
    
    # Create figure with subplots
    fig = plt.figure(figsize=figsize)
    gs = fig.add_gridspec(2, 2, hspace=0.3, wspace=0.3)
    
    # Main title
    fig.suptitle(f"DeepEval Test Results - Run: {filename}", 
                fontsize=16, fontweight='bold', y=0.98)
    
    # ============= 1. Test Cases Overview (Top-left) =============
    ax1 = fig.add_subplot(gs[0, 0])
    test_cases = data.get('testCases', [])
    
    if test_cases:
        # Count successes and failures
        successes = sum(tc.get('success', False) for tc in test_cases)
        failures = len(test_cases) - successes
        
        # Create pie chart
        colors = ['#2ecc71', '#e74c3c']
        wedges, texts, autotexts = ax1.pie(
            [successes, failures], 
            labels=['Passed', 'Failed'],
            colors=colors,
            autopct='%1.1f%%',
            startangle=90,
            explode=(0.05, 0.05),
            textprops={'fontsize': 10, 'fontweight': 'bold'}
        )
        ax1.set_title(f'Test Case Results (Total: {len(test_cases)})', 
                     fontsize=12, fontweight='bold')
    
    # ============= 2. Completion time Distribution (Top-right) =============
    ax2 = fig.add_subplot(gs[0, 1])
    durations = [tc['completionTime'] for tc in test_cases if 'completionTime' in tc]
    bins = range(0, math.ceil(max(durations)))
    ax2.hist(durations, bins=bins, color='#3498db', edgecolor='white', alpha=0.7)
    ax2.axvline(np.mean(durations), color='#e74c3c', linestyle='--', 
                label=f'Mean: {np.mean(durations):.2f}s')
    ax2.set_xlabel('Completion Time (seconds)', fontsize=10)
    ax2.set_ylabel('Frequency', fontsize=10)
    ax2.set_title('Time To Last Token Distribution', fontsize=12, fontweight='bold')
    ax2.legend(fontsize=8)
    ax2.grid(True, alpha=0.3)

    
    # ============= 3. Per-Metric Performance (Bottom-left) =============
    ax3 = fig.add_subplot(gs[1, 0])
    
    metrics = defaultdict(list)
    for test_case in test_cases:
        for metric in test_case.get('metricsData', []):
            if 'success' in metric:
                metrics[metric['name']].append(metric['success'])
    
    if metrics:
        # Stacked bar chart
        metric_names = list(metrics.keys())
        success_counts = [sum(metrics[name]) for name in metric_names]
        failure_counts = [len(metrics[name]) - sum(metrics[name]) for name in metric_names]
        x = np.arange(len(metric_names))
        ax3.bar(x, success_counts, color='#2ecc71', label='Successes')
        ax3.bar(x, failure_counts, bottom=success_counts, color='#e74c3c', label='Failures')
        ax3.set_xticks(x)
        ax3.set_xticklabels(metric_names, rotation=0, fontsize=8)
        ax3.set_ylabel('Count', fontsize=10)
        ax3.set_title('Per-Metric Performance', fontsize=12, fontweight='bold')
        ax3.legend(fontsize=8)
        ax3.grid(True, alpha=0.3, axis='y')

    
    # ============= 4. Summary Statistics =============
    ax4 = fig.add_subplot(gs[1, 1])
    ax4.axis('off')
    ax4.set_title('Summary Statistics', fontsize=12, fontweight='bold')

    ax4.text(0.5, 0.5, f"""
    Total Test Cases: {len(test_cases)}
    Passed: {sum(tc.get('success', False) for tc in test_cases)}
    Failed: {sum(not tc.get('success', False) for tc in test_cases)}
    Total Metrics: {sum(len(tc.get('metricsData', [])) for tc in test_cases)}
    """, fontsize=12, ha='center', va='center')

    
    # Save if requested
    if save_path:
        plt.savefig(save_path, dpi=300, bbox_inches='tight')
        print(f"✅ Figure saved to: {save_path}")
    
    plt.show()
    
    # Print detailed results
    print("\n" + "="*60)
    print("DETAILED TEST RESULTS")
    print("="*60)
    
    for i, test_case in enumerate(test_cases, 1):
        status = "✅ PASS" if test_case.get('success', False) else "❌ FAIL"
        print(f"\n{i}. {status} - {test_case.get('name', 'Unnamed Test')}")
        
        for metric in test_case.get('metricsData', []):
            score = metric.get('score', 0)
            threshold = metric.get('threshold', 0.7)
            status = "✓" if score >= threshold else "✗"
            print(f"   {status} {metric.get('name', 'Unknown')}: {score:.3f} (threshold: {threshold})")
    
    return data


# Example usage:
if __name__ == "__main__":
    path = "results\\20260319_195224"
    results = display_deepeval_results(json_path=path, save_path=None)